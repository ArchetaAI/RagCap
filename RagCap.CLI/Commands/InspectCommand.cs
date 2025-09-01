
using RagCap.Core.Pipeline;
using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class InspectCommand : Command
    {
        public InspectCommand() : base("inspect", "Inspect a RagCap capsule.")
        {
            var inputOption = new Option<string>("--input", "The path to the .ragcap file to inspect.");
            var jsonOption = new Option<bool>("--json", () => false, "Output the result as JSON.");

            AddOption(inputOption);
            AddOption(jsonOption);

            this.SetHandler(async (input, json) =>
            {
                await HandleInspect(input, json);
            }, inputOption, jsonOption);
        }

        private async Task HandleInspect(string input, bool json)
        {
            if (!System.IO.File.Exists(input))
            {
                Console.WriteLine($"Error: Capsule file not found at '{input}'");
                return;
            }

            var pipeline = new InspectPipeline(input);
            var result = await pipeline.RunAsync();

            if (json)
            {
                var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(jsonResult);
            }
            else
            {
                Console.WriteLine($"Capsule: {result.CapsulePath}");
                Console.WriteLine($"Provider: {result.Provider}");
                Console.WriteLine($"Model: {result.Model}");
                Console.WriteLine($"Dimension: {result.Dimension}");
                Console.WriteLine($"Sources: {result.Sources}");
                Console.WriteLine($"Chunks: {result.Chunks} (avg length {result.AvgChunkLength:F2} tokens)");
                Console.WriteLine($"Embeddings: {result.Embeddings}");
            }
        }

        public static Command Create()
        {
            return new InspectCommand();
        }
    }
}
