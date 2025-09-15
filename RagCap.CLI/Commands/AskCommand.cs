
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RagCap.Core.Generation;
using RagCap.Core.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace RagCap.CLI.Commands;

public class AskCommand : AsyncCommand<AskCommand.Settings>
{

    public sealed class Settings : CommandSettings
    {
        [Required]
        [CommandArgument(0, "<capsule_path>")]
        public required string CapsulePath { get; set; }

        [Required]
        [CommandArgument(1, "<question>")]
        public required string Question { get; set; }

        [CommandOption("--top-k")]
        [System.ComponentModel.DefaultValue(5)]
        public int TopK { get; set; }

        [CommandOption("--provider")]
        [System.ComponentModel.DefaultValue("local")]
        public string Provider { get; set; } = "local";

        [CommandOption("--json")]
        [System.ComponentModel.DefaultValue(false)]
        public bool Json { get; set; }

        [CommandOption("--model")]
        public string? Model { get; set; }

        [CommandOption("--api-key")]
        public string? ApiKey { get; set; }

        [CommandOption("--search-strategy")]
        [System.ComponentModel.DefaultValue("hybrid")]
        public string SearchStrategy { get; set; } = "hybrid";

        [CommandOption("--api-version")]
        public string? ApiVersion { get; set; }

        [CommandOption("--endpoint")]
        public string? Endpoint { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.CapsulePath))
        {
            AnsiConsole.WriteLine("Error: Capsule file not found");
            return 1;
        }

        var apiKey = settings.ApiKey ?? Environment.GetEnvironmentVariable("RAGCAP_API_KEY");

        AnsiConsole.WriteLine($"Debug: API Key used: {apiKey?.Substring(0, Math.Min(apiKey.Length, 5))}...");

        try
        {
            var pipeline = new AskPipeline(settings.CapsulePath);
            var (answer, sources) = await pipeline.ExecuteAsync(
                settings.Question,
                settings.TopK,
                settings.Provider,
                settings.Model ?? string.Empty,
                apiKey ?? string.Empty,
                settings.SearchStrategy,
                settings.ApiVersion,
                settings.Endpoint
            );

            if (settings.Json)
            {
                var result = new
                {
                    Answer = answer,
                    Sources = sources
                };
                AnsiConsole.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                AnsiConsole.WriteLine($"Answer: {answer}");
                if (sources.Any())
                {
                    AnsiConsole.WriteLine("Sources:");
                    foreach (var source in sources)
                    {
                        AnsiConsole.WriteLine($"- {source.SourceDocumentId} (Score: {source.Score:F4})");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"An error occurred: {ex.Message}");
            Debug.WriteLine(ex.ToString());
            return 1;
        }
    }
}
