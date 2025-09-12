
using RagCap.Export;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands;

public class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<CAPSULE_PATH>")]
        [Description("The path to the .ragcap file.")]
        public string CapsulePath { get; set; }

        [CommandOption("-f|--format")]
        [Description("The export format (parquet, faiss, hnsw).")]
        public string Format { get; set; }

        [CommandOption("-o|--output")]
        [Description("The output file path.")]
        public string OutputPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var exportManager = new ExportManager();
        await exportManager.ExportAsync(settings.CapsulePath, settings.OutputPath, settings.Format);
        AnsiConsole.WriteLine($"Capsule exported to {settings.OutputPath}");
        return 0;
    }
}
