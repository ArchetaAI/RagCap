using RagCap.Core.Capsule;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class DeleteSourceCommand : AsyncCommand<DeleteSourceCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule>")]
            public required string CapsulePath { get; set; }

            [CommandArgument(1, "<source>")]
            public required string Source { get; set; }

            [CommandOption("--by-id")]
            [DefaultValue(false)]
            public bool ById { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (!System.IO.File.Exists(settings.CapsulePath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Capsule file not found at '{settings.CapsulePath}'[/]");
                return -1;
            }

            using var capsule = new CapsuleManager(settings.CapsulePath);

            if (settings.ById)
            {
                if (!long.TryParse(settings.Source, out var id))
                {
                    AnsiConsole.MarkupLine($"[red]Error: '--by-id' specified but source '{settings.Source}' is not a valid numeric ID[/]");
                    return -1;
                }

                var (sources, chunks, embeddings) = await capsule.DeleteSourceByIdAsync(id);
                if (sources == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]No source found with id {id}. Nothing deleted.[/]");
                    return 0;
                }
                AnsiConsole.MarkupLine($"[green]Deleted source id {id}: {sources} source(s), {chunks} chunk(s), {embeddings} embedding(s).[/]");
            }
            else
            {
                var (sources, chunks, embeddings) = await capsule.DeleteSourceByPathAsync(settings.Source);
                if (sources == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]No source found with path '{settings.Source}'. Nothing deleted.[/]");
                    return 0;
                }
                AnsiConsole.MarkupLine($"[green]Deleted source(s) by path '{settings.Source}': {sources} source(s), {chunks} chunk(s), {embeddings} embedding(s).[/]");
            }

            return 0;
        }
    }
}
