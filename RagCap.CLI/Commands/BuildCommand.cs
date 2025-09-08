
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class BuildCommand : Command
    {
        public BuildCommand() : base("build", "Build a new RagCap capsule from a set of source documents.")
        {
            var inputOption = new Option<string>("--input", "The path to the source documents.");
            var outputOption = new Option<string>("--output", "The path to the .ragcap file to create.");
            var providerOption = new Option<string>("--provider", () => "local", "The embedding provider to use.");
            var modelOption = new Option<string>("--model", () => null, "The embedding model to use.");

            AddOption(inputOption);
            AddOption(outputOption);
            AddOption(providerOption);
            AddOption(modelOption);

            this.SetHandler(async (input, output, provider, model) =>
            {
                await HandleBuild(output, input, provider, model);
            }, inputOption, outputOption, providerOption, modelOption);
        }

        private async Task HandleBuild(string capsulePath, string sourcePath, string provider, string model)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                

                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Console.WriteLine("Error: RAGCAP_API_KEY environment variable must be set when using the API provider.");
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

                var pipeline = new Core.Pipeline.BuildPipeline(capsuleManager, embeddingProvider);
                await pipeline.RunAsync(sourcePath);

                Console.WriteLine($"Capsule saved: {capsulePath}");
            }
        }

        public static Command Create()
        {
            return new BuildCommand();
        }
    }
}
