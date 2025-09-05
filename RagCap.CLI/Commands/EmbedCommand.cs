using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class EmbedCommand : Command
    {
        public EmbedCommand() : base("embed", "Generate embeddings for a single file and add them to a capsule.")
        {
            var capsulePathArgument = new Argument<string>("capsule-path", "The path to the .ragcap file.");
            var sourceFileArgument = new Argument<string>("source-file", "The path to the source file.");

            var providerOption = new Option<string>("--provider", () => "local", "The embedding provider to use ('local' or 'api').");
            var apiProviderOption = new Option<string>("--api-provider", () => "openai", "The API provider to use ('openai' or 'azure').");
            var apiKeyOption = new Option<string>("--api-key", "The API key for the embedding provider.");

            AddArgument(capsulePathArgument);
            AddArgument(sourceFileArgument);
            AddOption(providerOption);
            AddOption(apiProviderOption);
            AddOption(apiKeyOption);

            this.SetHandler(async (invocationContext) =>
            {
                var capsulePath = invocationContext.ParseResult.GetValueForArgument(capsulePathArgument);
                var sourceFile = invocationContext.ParseResult.GetValueForArgument(sourceFileArgument);
                var provider = invocationContext.ParseResult.GetValueForOption(providerOption);
                var apiProvider = invocationContext.ParseResult.GetValueForOption(apiProviderOption);
                var apiKey = invocationContext.ParseResult.GetValueForOption(apiKeyOption);
                var cancellationToken = invocationContext.GetCancellationToken();
                await HandleEmbed(capsulePath, sourceFile, provider, apiProvider, apiKey, cancellationToken);
            });
        }

        private async Task HandleEmbed(string capsulePath, string sourceFile, string provider, string apiProvider, string apiKey, CancellationToken cancellationToken)
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
                        Console.Error.WriteLine("API key must be provided via --api-key or RAGCAP_API_KEY environment variable.");
                        return;
                    }
                    var apiEmbeddingProvider = new ApiEmbeddingProvider(apiProvider, finalApiKey);
                    embeddingProvider = apiEmbeddingProvider;
                    modelName = apiEmbeddingProvider.GetModelName();
                }
                else
                {
                    Console.Error.WriteLine($"Invalid provider: {provider}. Choose 'local' or 'api'.");
                    return;
                }

                await capsuleManager.SetMetaValueAsync("embedding_provider", provider);
                await capsuleManager.SetMetaValueAsync("embedding_model", modelName);

                var pipeline = new BuildPipeline(capsuleManager, embeddingProvider);
                await pipeline.RunAsync(sourceFile);

                Console.WriteLine($"Successfully embedded '{sourceFile}' into '{capsulePath}' using the '{provider}' provider.");
            }
        }

        public static Command Create()
        {
            return new EmbedCommand();
        }
    }
}