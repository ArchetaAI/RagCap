using RagCap.Core.Pipeline;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class SearchCommand : AsyncCommand<SearchCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule>")]
            public required string Capsule { get; set; }

            [CommandArgument(1, "<query>")]
            public required string Query { get; set; }

            [CommandOption("--top-k")]
            [DefaultValue(5)]
            public int TopK { get; set; }

            [CommandOption("--mode")]
            [DefaultValue("hybrid")]
            public string Mode { get; set; } = "hybrid";

            [CommandOption("--json")]
            [DefaultValue(false)]
            public bool Json { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleSearch(settings.Capsule, settings.Query, settings.TopK, settings.Mode, settings.Json);
            return 0;
        }

        private async Task HandleSearch(string capsule, string query, int topK, string mode, bool json)
        {
            if (!System.IO.File.Exists(capsule))
            {
                AnsiConsole.MarkupLine($"[red]Error: Capsule file not found at '{capsule}'[/]");
                return;
            }

            try
            {
                var pipeline = new SearchPipeline(capsule);
                var results = await pipeline.RunAsync(query, topK, mode);

                if (json)
                {
                    var jsonResult = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                    AnsiConsole.WriteLine(jsonResult);
                }
                else
                {
                    foreach (var result in results)
                    {
                        AnsiConsole.WriteLine($"Result from '{result.Source}' (chunk {result.ChunkId}, score: {result.Score:F4}):");
                        AnsiConsole.WriteLine(result.Text ?? string.Empty);
                        AnsiConsole.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        }
    }
}
