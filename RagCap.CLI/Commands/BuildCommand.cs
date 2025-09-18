using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using RagCap.Core.Recipes;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using RagCap.Core.Pipeline;
using System;
using RagCap.Core.Utils;

namespace RagCap.CLI.Commands
{
    public class BuildCommand : AsyncCommand<BuildCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--input")]
            public string? Input { get; set; }

            [CommandOption("--output")]
            public string? Output { get; set; }

            [CommandOption("--provider")]
            public string? Provider { get; set; }

            [CommandOption("--model")]
            public string? Model { get; set; }

            [CommandOption("--recipe")]
            public string? RecipePath { get; set; }

            [CommandOption("--api-version")]
            public string? ApiVersion { get; set; }

            [CommandOption("--endpoint")]
            public string? Endpoint { get; set; }

            [CommandOption("--verbose")]
            [DefaultValue(false)]
            public bool Verbose { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            Recipe? recipe = null;
            if (!string.IsNullOrEmpty(settings.RecipePath))
            {
                if (!File.Exists(settings.RecipePath))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Recipe file not found at '{settings.RecipePath}'[/]");
                    return 1;
                }

                var recipeContent = await File.ReadAllTextAsync(settings.RecipePath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                try
                {
                    recipe = deserializer.Deserialize<Recipe>(recipeContent);
                }
                catch (System.Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing recipe file:[/]");
                    AnsiConsole.WriteException(ex);
                    return 1;
                }
            }

            var config = ConfigManager.GetConfig();

            var inputPath = settings.Input ?? recipe?.Sources?.FirstOrDefault()?.Path;
            var outputPath = settings.Output ?? recipe?.Output?.Path;
            var provider = settings.Provider ?? Environment.GetEnvironmentVariable("RAGCAP_EMBEDDING_PROVIDER") ?? config.Embedding?.Provider ?? recipe?.Embeddings?.Provider ?? "local";
            var model = settings.Model ?? Environment.GetEnvironmentVariable("RAGCAP_EMBEDDING_MODEL") ?? config.Embedding?.Model ?? recipe?.Embeddings?.Model;
            var apiVersion = settings.ApiVersion ?? Environment.GetEnvironmentVariable("RAGCAP_API_VERSION") ?? config.Api?.ApiVersion ?? recipe?.Embeddings?.ApiVersion;
            var endpoint = settings.Endpoint ?? Environment.GetEnvironmentVariable("RAGCAP_ENDPOINT") ?? Environment.GetEnvironmentVariable("RAGCAP_AZURE_ENDPOINT") ?? config.Api?.Endpoint ?? recipe?.Embeddings?.Endpoint;

            if (string.IsNullOrEmpty(inputPath))
            {
                AnsiConsole.MarkupLine("[red]Error: Input path must be specified via --input or recipe file.[/]");
                return 1;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                AnsiConsole.MarkupLine("[red]Error: Output path must be specified via --output or recipe file.[/]");
                return 1;
            }

            await HandleBuild(outputPath, inputPath, provider, model, apiVersion, endpoint, recipe, settings.RecipePath, settings.Verbose);
            return 0;
        }

        private async Task HandleBuild(string capsulePath, string sourcePath, string provider, string? model, string? apiVersion, string? endpoint, Recipe? recipe, string? recipePath, bool verbose)
        {
            RagCap.Core.Utils.Logging.Verbose = verbose;
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var config = ConfigManager.GetConfig();
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY") ?? config.Api?.ApiKey;
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        AnsiConsole.MarkupLine("[red]Error: RAGCAP_API_KEY environment variable or config file entry must be set when using the API provider.[/]");
                        return;
                    }
                    embeddingProvider = new ApiEmbeddingProvider(apiKey, model, endpoint, apiVersion);
                }
                else
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                    // For local provider, we currently ship the ONNX model all-MiniLM-L6-v2
                    // Record this explicitly regardless of config/env defaults.
                    model = "all-MiniLM-L6-v2";
                }

                await capsuleManager.SetMetaValueAsync("embedding_provider", provider);
                if (!string.IsNullOrEmpty(model))
                {
                    await capsuleManager.SetMetaValueAsync("embedding_model", model);
                }
                if (!string.IsNullOrEmpty(apiVersion))
                {
                    await capsuleManager.SetMetaValueAsync("embedding_api_version", apiVersion);
                }
                if (!string.IsNullOrEmpty(endpoint))
                {
                    await capsuleManager.SetMetaValueAsync("embedding_endpoint", endpoint);
                }

                if (recipe != null && !string.IsNullOrEmpty(recipePath))
                {
                    var recipeContent = await File.ReadAllTextAsync(recipePath);
                    await capsuleManager.SetMetaValueAsync("recipe", recipeContent);
                }

                var pipeline = new BuildPipeline(capsuleManager, embeddingProvider, recipe);
                var sourcesFromRecipe = recipe?.Sources?.Select(s => s.Path).ToList();
                await pipeline.RunAsync(sourcePath, sourcesFromRecipe);

                AnsiConsole.MarkupLine($"[green]Capsule saved:[/] {capsulePath}");
            }
        }
    }
}
