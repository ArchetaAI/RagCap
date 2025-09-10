
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
        public string CapsulePath { get; set; }

        [Required]
        [CommandArgument(1, "<question>")]
        public string Question { get; set; }

        [CommandOption("--top-k")]
        [System.ComponentModel.DefaultValue(5)]
        public int TopK { get; set; }

        [CommandOption("--provider")]
        [System.ComponentModel.DefaultValue("local")]
        public string Provider { get; set; }

        [CommandOption("--json")]
        [System.ComponentModel.DefaultValue(false)]
        public bool Json { get; set; }

        [CommandOption("--model")]
        public string Model { get; set; }

        [CommandOption("--api-key")]
        public string ApiKey { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.CapsulePath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Capsule file not found at '{settings.CapsulePath}'[/]");
            return 1;
        }

        var apiKey = settings.ApiKey ?? Environment.GetEnvironmentVariable("RAGCAP_API_KEY");

        try
        {
            var pipeline = new AskPipeline(settings.CapsulePath);
            var (answer, sources) = await pipeline.ExecuteAsync(
                settings.Question,
                settings.TopK,
                settings.Provider,
                settings.Model,
                apiKey
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
                AnsiConsole.MarkupLine($"[green]Answer:[/] {answer}");
                if (sources.Any())
                {
                    AnsiConsole.MarkupLine("
[yellow]Sources:[/]");
                    foreach (var source in sources)
                    {
                        AnsiConsole.MarkupLine($"- [cyan]{source.SourceDocumentId}[/] (Score: {source.Score:F4})");
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]An error occurred: {ex.Message}[/]");
            Debug.WriteLine(ex.ToString());
            return 1;
        }
    }
}
