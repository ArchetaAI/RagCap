
using RagCap.Export;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using System.IO;

namespace RagCap.CLI.Commands;

public class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<CAPSULE_PATH>")]
        [Description("The path to the .ragcap file.")]
        public required string CapsulePath { get; set; }

        [CommandOption("-f|--format")]
        [Description("The export format (parquet, faiss, hnsw).")]
        public string? Format { get; set; }

        [CommandOption("-o|--output")]
        [Description("The output file path.")]
        public string? OutputPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var exportManager = new ExportManager();

        var format = (settings.Format ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(format))
        {
            format = "parquet";
        }

        // Resolve the output path: allow a directory path and create missing folders.
        string resolvedOutputPath;
        var capsuleFullPath = Path.GetFullPath(settings.CapsulePath);
        var capsuleDir = Path.GetDirectoryName(capsuleFullPath) ?? ".";
        var capsuleName = Path.GetFileNameWithoutExtension(capsuleFullPath);

        string DefaultExtension() => format.ToLowerInvariant() switch
        {
            "parquet" => ".parquet",
            "faiss" => ".faiss",
            "hnsw" => ".hnsw",
            _ => ".out"
        };

        if (string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            resolvedOutputPath = Path.Combine(capsuleDir, capsuleName + DefaultExtension());
        }
        else
        {
            var candidate = settings.OutputPath!;
            // If the provided output is an existing directory or ends with a separator, treat it as a directory
            var isDirectory = Directory.Exists(candidate) || Path.EndsInDirectorySeparator(candidate);
            if (isDirectory)
            {
                resolvedOutputPath = Path.Combine(candidate, capsuleName + DefaultExtension());
            }
            else
            {
                resolvedOutputPath = candidate;
            }
        }

        var outDir = Path.GetDirectoryName(Path.GetFullPath(resolvedOutputPath));
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        await exportManager.ExportAsync(settings.CapsulePath, resolvedOutputPath, format);
        AnsiConsole.WriteLine($"Capsule exported to {resolvedOutputPath}");
        return 0;
    }
}
