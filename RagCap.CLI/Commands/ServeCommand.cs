
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class ServeCommand : AsyncCommand<ServeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule_path>")]
            public string CapsulePath { get; set; }

            [CommandOption("--port")]
            [DefaultValue(8080)]
            public int Port { get; set; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Serve command is not yet implemented.");
            return Task.FromResult(0);
        }
    }
}
