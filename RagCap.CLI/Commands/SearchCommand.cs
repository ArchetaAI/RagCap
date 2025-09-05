
using RagCap.Core.Pipeline;
using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class SearchCommand : Command
    {
        public SearchCommand() : base("search", "Search a RagCap capsule.")
        {
            var capsuleArgument = new Argument<string>("capsule", "The path to the .ragcap file.");
            var queryArgument = new Argument<string>("query", "The search query.");

            var topKOption = new Option<int>("--top-k", () => 5, "The number of results to return.");
            var modeOption = new Option<string>("--mode", () => "hybrid", "The search mode (vector, bm25, or hybrid).");
            var jsonOption = new Option<bool>("--json", () => false, "Output the result as JSON.");

            AddArgument(capsuleArgument);
            AddArgument(queryArgument);
            AddOption(topKOption);
            AddOption(modeOption);
            AddOption(jsonOption);

            this.SetHandler(async (capsule, query, topK, mode, json) =>
            {
                await HandleSearch(capsule, query, topK, mode, json);
            }, capsuleArgument, queryArgument, topKOption, modeOption, jsonOption);
        }

        private async Task HandleSearch(string capsule, string query, int topK, string mode, bool json)
        {
            if (!System.IO.File.Exists(capsule))
            {
                Console.WriteLine($"Error: Capsule file not found at '{capsule}'");
                return;
            }

            try
            {
                var pipeline = new SearchPipeline(capsule);
                var results = await pipeline.RunAsync(query, topK, mode);

                if (json)
                {
                    var jsonResult = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(jsonResult);
                }
                else
                {
                    foreach (var result in results)
                    {
                        Console.WriteLine($"Result from '{result.Source}' (chunk {result.ChunkId}, score: {result.Score:F4}):");
                        Console.WriteLine(result.Text);
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public static Command Create()
        {
            return new SearchCommand();
        }
    }
}
