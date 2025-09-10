
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class EmbedCommand : AsyncCommand<EmbedCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule-path>")]
            public string CapsulePath { get; set; }

            [CommandArgument(1, "<source-file>")]
            public string SourceFile { get; set; }

            [CommandOption("--provider")]
            [DefaultValue("local")]
            public string Provider { get; set; }

            [CommandOption("--api-provider")]
            [DefaultValue("openai")]
            public string ApiProvider { get; set; }

            [CommandOption("--api-key")]
            public string ApiKey { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleEmbed(settings.CapsulePath, settings.SourceFile, settings.Provider, settings.ApiProvider, settings.ApiKey);
            return 0;
        }

        private async Task HandleEmbed(string capsulePath, string sourceFile, string provider, string apiProvider, string apiKey)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                IEmbeddingProvider embeddingProvider = null;
                string modelName = "default";

                if (provider.Equals("local", StringComparison.OrdinalIgnoreCase))
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                    modelName = "all-MiniLM-L6-v2"; // Example model name
                }
                else if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var finalApiKey = apiKey ?? Environment.GetEnvironmentVariable("RAGCAP_API_KEY");
                    if (string.IsNullOrEmpty(finalApiKey))
                    {
                        AnsiConsole.MarkupLine("[red]API key must be provided via --api-key or RAGCAP_API_KEY environment variable.[/]");
                        return;
                    }
                    var apiEmbeddingProvider = new ApiEmbeddingProvider(apiProvider, finalApiKey);
                    embeddingProvider = apiEmbeddingProvider;
                    modelName = apiEmbeddingProvider.GetModelName();
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Invalid provider: {provider}. Choose 'local' or 'api'.[/]");
                    return;
                }

                await capsuleManager.SetMetaValueAsync("embedding_provider", provider);
                await capsuleManager.SetMetaValueAsync("embedding_model", modelName);

                var pipeline = new BuildPipeline(capsuleManager, embeddingProvider);
                await pipeline.RunAsync(sourceFile);

                AnsiConsole.MarkupLine($"[green]Successfully embedded '{sourceFile}' into '{capsulePath}' using the '{provider}' provider.[/]");
            }
        }
    }
}
