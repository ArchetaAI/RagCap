param(
  [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  if ($PSScriptRoot) { return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
  return (Resolve-Path '..').Path
}

$root = Get-RepoRoot
Write-Host "Repo root: $root"

if (-not $Version) {
  $propsPath = Join-Path $root 'Directory.Build.props'
  if (Test-Path $propsPath) {
    try {
      [xml]$x = Get-Content -Raw -Path $propsPath
      $Version = $x.Project.PropertyGroup.Version
    } catch {
      Write-Warning "Could not read Version from Directory.Build.props: $_"
    }
  }
}
if (-not $Version) { $Version = '0.1.0-local' }

Write-Host "Using package version: $Version"

$nupkgs = Join-Path $root 'artifacts\nupkgs'
if (-not (Test-Path $nupkgs)) { New-Item -ItemType Directory -Force -Path $nupkgs | Out-Null }

Push-Location $root
try {
  Write-Host 'Restoring solution...'
  dotnet restore .\RagCap.sln

  $packArgs = @('-c','Release','-o',$nupkgs,"/p:Version=$Version")

  Write-Host 'Packing RagCap.Core...'
  dotnet pack .\RagCap.Core\RagCap.Core.csproj @packArgs

  Write-Host 'Packing RagCap.Export...'
  dotnet pack .\RagCap.Export\RagCap.Export.csproj @packArgs

  Write-Host 'Packing RagCap.CLI (tool)...'
  dotnet pack .\RagCap.CLI\RagCap.CLI.csproj @packArgs

  Write-Host 'Packages created:'
  Get-ChildItem $nupkgs -Filter *.nupkg | Select-Object Name,Length | Format-Table -AutoSize | Out-String | Write-Host

  # Quick content inspection for the CLI package
  $cliPkg = Join-Path $nupkgs ("RagCap.CLI.Tool.$Version.nupkg")
  if (Test-Path $cliPkg) {
    $inspect = Join-Path $root 'artifacts\inspect\cli'
    if (Test-Path $inspect) { Remove-Item -Recurse -Force $inspect }
    New-Item -ItemType Directory -Force -Path $inspect | Out-Null
    # Expand-Archive only supports .zip; copy .nupkg to .zip for inspection
    $cliZip = Join-Path $nupkgs ("RagCap.CLI.Tool.$Version.zip")
    Copy-Item -Path $cliPkg -Destination $cliZip -Force
    try {
      Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
      [System.IO.Compression.ZipFile]::ExtractToDirectory($cliZip, $inspect)
      Write-Host "Expanded CLI package to: $inspect"
    }
    catch {
      Write-Warning "Could not extract package for inspection: $_"
    }
    if (Test-Path (Join-Path $inspect 'tools\net8.0\any\models')) {
      Write-Host 'Found embedded models folder in CLI package.'
    } else {
      Write-Warning 'models folder not found in CLI package. Verify content includes expected assets.'
    }
  }

  # Test install and run CLI tool from local package
  $toolsDir = Join-Path $root '.tools'
  if (-not (Test-Path $toolsDir)) { New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null }
  Write-Host 'Installing CLI tool locally from folder source...'
  try { dotnet tool uninstall --tool-path $toolsDir RagCap.CLI.Tool | Out-Null } catch { }
  dotnet tool install --tool-path $toolsDir --add-source $nupkgs RagCap.CLI.Tool --version $Version
  & (Join-Path $toolsDir 'ragcap') --help | Write-Host

  # Test consuming RagCap.Core from a throwaway console app
  $testDir = Join-Path $root 'artifacts\tests\CoreTest'
  if (Test-Path $testDir) { Remove-Item -Recurse -Force $testDir }
  Write-Host 'Creating test console project for RagCap.Core...'
  dotnet new console -n CoreTest -o $testDir
  Write-Host 'Adding RagCap.Core package from local source...'
  dotnet add (Join-Path $testDir 'CoreTest.csproj') package RagCap.Core --version $Version --source $nupkgs
  Write-Host 'Building CoreTest...'
  dotnet build $testDir -c Release
  Write-Host 'CoreTest build complete.'
}
finally {
  Pop-Location
}

Write-Host 'Pack and local test workflow finished.'
