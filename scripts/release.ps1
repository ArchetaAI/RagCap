param(
  [Parameter(Mandatory=$true)][string]$Version,
  [switch]$Publish,
  [string]$ApiKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function RepoRoot {
  if ($PSScriptRoot) { return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
  return (Resolve-Path '..').Path
}

$root = RepoRoot
Push-Location $root
try {
  Write-Host "RagCap release script starting for version $Version" -ForegroundColor Cyan

  $nupkgs = Join-Path $root 'artifacts\nupkgs'
  if (Test-Path $nupkgs) { Remove-Item -Recurse -Force $nupkgs }
  New-Item -ItemType Directory -Force -Path $nupkgs | Out-Null

  Write-Host 'Restoring solution...'
  dotnet restore .\RagCap.sln

  $packArgs = @('-c','Release','-o',$nupkgs,"/p:Version=$Version")
  Write-Host 'Packing RagCap.Core...'
  dotnet pack .\RagCap.Core\RagCap.Core.csproj @packArgs
  Write-Host 'Packing RagCap.Export...'
  dotnet pack .\RagCap.Export\RagCap.Export.csproj @packArgs
  Write-Host 'Packing RagCap.CLI (tool)...'
  dotnet pack .\RagCap.CLI\RagCap.CLI.csproj @packArgs

  Write-Host 'Packages:' -ForegroundColor Green
  Get-ChildItem $nupkgs -Filter *.nupkg | Select-Object Name,Length | Format-Table -AutoSize | Out-String | Write-Host

  # Quick install test for the CLI from local folder
  $toolsDir = Join-Path $root '.tools'
  New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
  try { dotnet tool uninstall --tool-path $toolsDir RagCap.CLI.Tool | Out-Null } catch { }
  dotnet tool install --tool-path $toolsDir --add-source $nupkgs RagCap.CLI.Tool --version $Version
  & (Join-Path $toolsDir 'ragcap') --help | Out-Host

  if ($Publish) {
    $key = if ($ApiKey) { $ApiKey } else { $env:NUGET_API_KEY }
    if (-not $key) { throw 'NuGet API key not provided. Use -ApiKey or set NUGET_API_KEY.' }
    $source = 'https://api.nuget.org/v3/index.json'
    Write-Host 'Pushing packages to NuGet...' -ForegroundColor Yellow
    $ids = @('RagCap.Core','RagCap.Export','RagCap.CLI.Tool')
    foreach ($id in $ids) {
      $pkg = Join-Path $nupkgs ("$id.$Version.nupkg")
      if (!(Test-Path $pkg)) { throw "Missing package: $pkg" }
      dotnet nuget push $pkg -k $key -s $source --skip-duplicate
    }
  }

  # Self-contained publish artifacts
  $publishRoot = Join-Path $root 'artifacts\publish'
  $releaseRoot = Join-Path $root 'artifacts\release'
  New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
  New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

  $rids = @('win-x64','linux-x64','osx-x64','osx-arm64')
  foreach ($rid in $rids) {
    Write-Host "Publishing self-contained for $rid..." -ForegroundColor Cyan
    $outDir = Join-Path $publishRoot $rid
    dotnet publish .\RagCap.CLI\RagCap.CLI.csproj -c Release -r $rid --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false -o $outDir
    # Ensure local model assets are present for local provider
    $modelsSrc = Join-Path $root 'RagCap.Core\models'
    if (Test-Path $modelsSrc) {
      New-Item -ItemType Directory -Force -Path (Join-Path $outDir 'models') | Out-Null
      Copy-Item -Recurse -Force -Path (Join-Path $modelsSrc '*') -Destination (Join-Path $outDir 'models')
    }
    $zipName = "ragcap-$rid-$Version.zip"
    $zipPath = Join-Path $releaseRoot $zipName
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath
  }

  # Checksums
  $checksums = Join-Path $releaseRoot 'SHA256SUMS.txt'
  if (Test-Path $checksums) { Remove-Item -Force $checksums }
  Get-ChildItem $releaseRoot -Filter *.zip | ForEach-Object {
    $h = Get-FileHash $_.FullName -Algorithm SHA256
    "{0}  {1}" -f $h.Hash, $_.Name | Add-Content $checksums
  }

  Write-Host "Release artifacts ready in: $releaseRoot" -ForegroundColor Green
  Write-Host "Next steps:" -ForegroundColor Yellow
  Write-Host "1) (optional) Publish now: scripts/release.ps1 -Version $Version -Publish -ApiKey <NUGET_API_KEY>"
  Write-Host "2) Upload zipped binaries from artifacts/release to your GitHub Release"
  Write-Host "3) Tag: git tag v$Version && git push origin v$Version"
}
finally {
  Pop-Location
}
