using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RagCap.Core.Generation;
using RagCap.Core.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using RagCap.Core.Utils;

namespace RagCap.CLI.Commands;

/// <summary>
/// Represents the command for asking a question to a RagCap capsule.
/// </summary>
public class AskCommand : AsyncCommand<AskCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the ask command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the capsule file.
        /// </summary>
        [Required]
        [CommandArgument(0, "<capsule_path>")]
        public required string CapsulePath { get; set; }

        /// <summary>
        /// Gets or sets the question to ask.
        /// </summary>
        [Required]
        [CommandArgument(1, "<question>")]
        public required string Question { get; set; }

        /// <summary>
        /// Gets or sets the number of top results to return.
        /// </summary>
        [CommandOption("--top-k")]
        [System.ComponentModel.DefaultValue(5)]
        public int TopK { get; set; }

        /// <summary>
        /// Gets or sets the provider for answer generation.
        /// </summary>
        [CommandOption("--provider")]
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to output the result as JSON.
        /// </summary>
        [CommandOption("--json")]
        [System.ComponentModel.DefaultValue(false)]
        public bool Json { get; set; }

        /// <summary>
        /// Gets or sets the model for answer generation.
        /// </summary>
        [CommandOption("--model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the API key.
        /// </summary>
        [CommandOption("--api-key")]
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the search strategy.
        /// </summary>
        [CommandOption("--search-strategy")]
        [System.ComponentModel.DefaultValue("hybrid")]
        public string SearchStrategy { get; set; } = "hybrid";

        /// <summary>
        /// Gets or sets the API version.
        /// </summary>
        [CommandOption("--api-version")]
        public string? ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets the API endpoint.
        /// </summary>
        [CommandOption("--endpoint")]
        public string? Endpoint { get; set; }
    }

    /// <summary>
    /// Executes the ask command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>The exit code.</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.CapsulePath))
        {
            AnsiConsole.WriteLine("Error: Capsule file not found");
            return 1;
        }

        var config = ConfigManager.GetConfig();

        var apiKey = settings.ApiKey ?? Environment.GetEnvironmentVariable("RAGCAP_API_KEY") ?? config.Api?.ApiKey;
        var provider = settings.Provider ?? Environment.GetEnvironmentVariable("RAGCAP_ANSWER_PROVIDER") ?? config.Answer?.Provider ?? "local";
        var model = settings.Model ?? Environment.GetEnvironmentVariable("RAGCAP_ANSWER_MODEL") ?? config.Answer?.Model;
        var apiVersion = settings.ApiVersion ?? Environment.GetEnvironmentVariable("RAGCAP_API_VERSION") ?? config.Api?.ApiVersion;
        var endpoint = settings.Endpoint ?? Environment.GetEnvironmentVariable("RAGCAP_ENDPOINT") ?? Environment.GetEnvironmentVariable("RAGCAP_AZURE_ENDPOINT") ?? config.Api?.Endpoint;

        try
        {
            var pipeline = new AskPipeline(settings.CapsulePath);
            var (answer, sources) = await pipeline.ExecuteAsync(
                settings.Question,
                settings.TopK,
                provider,
                model ?? string.Empty,
                apiKey ?? string.Empty,
                settings.SearchStrategy,
                apiVersion,
                endpoint
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