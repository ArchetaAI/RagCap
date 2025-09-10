
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class DiffCommand : AsyncCommand<DiffCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<file_a>")]
            public string FileA { get; set; }

            [CommandArgument(1, "<file_b>")]
            public string FileB { get; set; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Diff command is not yet implemented.");
            return Task.FromResult(0);
        }
    }
}
