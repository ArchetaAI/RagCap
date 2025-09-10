
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class ExportCommand : AsyncCommand<ExportCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule_path>")]
            public string CapsulePath { get; set; }

            [CommandOption("--format")]
            [DefaultValue("parquet")]
            public string Format { get; set; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Export command is not yet implemented.");
            return Task.FromResult(0);
        }
    }
}
