using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RagCap.CLI.Commands
{
    public class BuildCommand : AsyncCommand<BuildCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--input")]
            public string Input { get; set; }

            [CommandOption("--output")]
            public string Output { get; set; }

            [CommandOption("--provider")]
            [DefaultValue("local")]
            public string Provider { get; set; }

            [CommandOption("--model")]
            public string Model { get; set; }

            [CommandOption("--recipe")]
            public string RecipePath { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            Recipe recipe = null;
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

            var inputPath = settings.Input ?? recipe?.Sources?.FirstOrDefault()?.Path;
            var outputPath = settings.Output ?? recipe?.Output?.Path;
            var provider = settings.Provider ?? recipe?.Embeddings?.Provider ?? "local";
            var model = settings.Model ?? recipe?.Embeddings?.Model;

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

            await HandleBuild(outputPath, inputPath, provider, model, recipe, settings.RecipePath);
            return 0;
        }

        private async Task HandleBuild(string capsulePath, string sourcePath, string provider, string model, Recipe recipe, string recipePath)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        AnsiConsole.MarkupLine("[red]Error: RAGCAP_API_KEY environment variable must be set when using the API provider.[/]");
                        return;
                    }
                    embeddingProvider = new ApiEmbeddingProvider(model, apiKey);
                }
                else
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                }

                await capsuleManager.SetMetaValueAsync("embedding_provider", provider);
                if (!string.IsNullOrEmpty(model))
                {
                    await capsuleManager.SetMetaValueAsync("embedding_model", model);
                }

                if (recipe != null && !string.IsNullOrEmpty(recipePath))
                {
                    var recipeContent = await File.ReadAllTextAsync(recipePath);
                    await capsuleManager.SetMetaValueAsync("recipe", recipeContent);
                }

                var pipeline = new Core.Pipeline.BuildPipeline(capsuleManager, embeddingProvider, recipe);
                var sourcesFromRecipe = recipe?.Sources?.Select(s => s.Path).ToList();
                await pipeline.RunAsync(sourcePath, sourcesFromRecipe);

                AnsiConsole.MarkupLine($"[green]Capsule saved:[/] {capsulePath}");
            }
        }
    }
}
