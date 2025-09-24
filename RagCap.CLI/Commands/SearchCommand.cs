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

            [CommandOption("--candidate-limit")]
            [DefaultValue(500)]
            public int CandidateLimit { get; set; } = 500;

            [CommandOption("--json")]
            [DefaultValue(false)]
            public bool Json { get; set; }

            // Optional VSS overrides
            [CommandOption("--vss-path")]
            public string? VssPath { get; set; }

            [CommandOption("--vss-module")]
            public string? VssModule { get; set; }

            [CommandOption("--vss-search-func")]
            public string? VssSearchFunc { get; set; }

            [CommandOption("--vss-fromblob-func")]
            public string? VssFromBlobFunc { get; set; }

            // Optional sqlite-vec overrides
            [CommandOption("--vec-path")]
            public string? VecPath { get; set; }

            [CommandOption("--vec-module")]
            public string? VecModule { get; set; }

            [CommandOption("--include-path")]
            public string? IncludePath { get; set; }

            [CommandOption("--exclude-path")]
            public string? ExcludePath { get; set; }

            [CommandOption("--mmr")]
            [DefaultValue(false)]
            public bool Mmr { get; set; }

            [CommandOption("--mmr-lambda")]
            [DefaultValue(0.5f)]
            public float MmrLambda { get; set; } = 0.5f;

            [CommandOption("--mmr-pool")]
            [DefaultValue(50)]
            public int MmrPool { get; set; } = 50;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleSearch(settings.Capsule, settings.Query, settings.TopK, settings.Mode, settings.CandidateLimit, settings.Json,
                settings.VssPath, settings.VssModule, settings.VssSearchFunc, settings.VssFromBlobFunc,
                settings.VecPath, settings.VecModule,
                settings.IncludePath, settings.ExcludePath,
                settings.Mmr, settings.MmrLambda, settings.MmrPool);
            return 0;
        }

        private async Task HandleSearch(string capsule, string query, int topK, string mode, int candidateLimit, bool json,
            string? vssPath, string? vssModule, string? vssSearchFunc, string? vssFromBlobFunc,
            string? vecPath, string? vecModule,
            string? includePath, string? excludePath,
            bool mmr, float mmrLambda, int mmrPool)
        {
            if (!System.IO.File.Exists(capsule))
            {
                AnsiConsole.MarkupLine($"[red]Error: Capsule file not found at '{capsule}'[/]");
                return;
            }

            try
            {
                var pipeline = new SearchPipeline(capsule);
                var vss = new RagCap.Core.Search.VssOptions
                {
                    Path = vssPath,
                    Module = vssModule,
                    SearchFunction = vssSearchFunc,
                    FromBlobFunction = vssFromBlobFunc
                };
                var vec = new RagCap.Core.Search.VecOptions
                {
                    Path = vecPath,
                    Module = vecModule ?? "vec0"
                };
                var results = await pipeline.RunAsync(query, topK, mode, candidateLimit, vss, vec, includePath, excludePath, mmr, mmrLambda, mmrPool);

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
