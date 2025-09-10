using RagCap.Core.Pipeline;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class InspectCommand : AsyncCommand<InspectCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<input>")]
            public string Input { get; set; }

            [CommandOption("--json")]
            [DefaultValue(false)]
            public bool Json { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleInspect(settings.Input, settings.Json);
            return 0;
        }

        private async Task HandleInspect(string input, bool json)
        {
            if (!System.IO.File.Exists(input))
            {
                AnsiConsole.MarkupLine($"[red]Error: Capsule file not found at '{input}'[/]");
                return;
            }

            var pipeline = new InspectPipeline(input);
            var result = await pipeline.RunAsync();

            if (json)
            {
                var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                AnsiConsole.WriteLine(jsonResult);
            }
            else
            {
                AnsiConsole.WriteLine($"Capsule: {result.CapsulePath}");
                AnsiConsole.WriteLine($"Provider: {result.Provider}");
                AnsiConsole.WriteLine($"Model: {result.Model}");
                AnsiConsole.WriteLine($"Dimension: {result.Dimension}");
                AnsiConsole.WriteLine($"Sources: {result.Sources}");
                AnsiConsole.WriteLine($"Chunks: {result.Chunks} (avg length {result.AvgChunkLength:F2} tokens)");
                AnsiConsole.WriteLine($"Embeddings: {result.Embeddings}");
            }
        }
    }
}