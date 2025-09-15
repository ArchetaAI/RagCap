using RagCap.Core.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text.Json;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class DiffCommand : AsyncCommand<DiffCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<CAPSULE1_PATH>")]
            public required string Capsule1Path { get; set; }

            [CommandArgument(1, "<CAPSULE2_PATH>")]
            public required string Capsule2Path { get; set; }

            [CommandOption("--json")]
            public bool Json { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var pipeline = new DiffPipeline(settings.Capsule1Path, settings.Capsule2Path);
            var result = await pipeline.RunAsync();

            if (settings.Json)
            {
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                AnsiConsole.WriteLine(json);
            }
            else
            {
                PrintHumanReadable(result, settings.Capsule1Path, settings.Capsule2Path);
            }

            return 0;
        }

        private void PrintHumanReadable(DiffResult result, string capsule1, string capsule2)
        {
            AnsiConsole.MarkupLine($"[bold]Comparing:[/]{capsule1}");
            AnsiConsole.MarkupLine($"[bold]To:[/]{capsule2}\n");

            var table = new Table().Expand().Border(TableBorder.None);
            table.AddColumn("[bold]Category[/]");
            table.AddColumn("[bold]Property[/]");
            table.AddColumn($"[bold]{Path.GetFileName(capsule1)}[/]");
            table.AddColumn($"[bold]{Path.GetFileName(capsule2)}[/]");

            foreach (var item in result.Manifest)
            {
                table.AddRow("Manifest", item.Key, item.Value.Item1 ?? string.Empty, item.Value.Item2 ?? string.Empty);
            }

            if (result.AddedSources.Any())
                table.AddRow("Sources", "Added", "", string.Join("\n", result.AddedSources));
            if (result.RemovedSources.Any())
                table.AddRow("Sources", "Removed", string.Join("\n", result.RemovedSources), "");
            if (result.ModifiedSources.Any())
                table.AddRow("Sources", "Modified", string.Join("\n", result.ModifiedSources), string.Join("\n", result.ModifiedSources));

            table.AddRow("Chunks", "Count", result.ChunkCount.Item1.ToString(), result.ChunkCount.Item2.ToString());
            table.AddRow("Chunks", "Avg Size", $"{result.AverageChunkSize.Item1:F2}", $"{result.AverageChunkSize.Item2:F2}");

            table.AddRow("Embeddings", "Dimensions", result.EmbeddingDimensions.Item1.ToString(), result.EmbeddingDimensions.Item2.ToString());

            if (result.Recipe.Item1 != result.Recipe.Item2)
            {
                table.AddRow("Recipe", "", "Recipe differs", "Recipe differs");
            }

            AnsiConsole.Write(table);
        }
    }
}
