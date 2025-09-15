using RagCap.Core.Validation;
using Spectre.Console.Cli;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class VerifyCommand : AsyncCommand<VerifyCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<file>")]
            public required string File { get; set; }
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var validator = new CapsuleValidator();
            var result = validator.Validate(settings.File);

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"[green]✔ {result.Message}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✘ {result.Message}[/]");
            }
            return Task.FromResult(0);
        }
    }
}
