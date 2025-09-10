using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;

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
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleBuild(settings.Output, settings.Input, settings.Provider, settings.Model);
            return 0;
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

                var pipeline = new Core.Pipeline.BuildPipeline(capsuleManager, embeddingProvider);
                await pipeline.RunAsync(sourcePath);

                AnsiConsole.MarkupLine($"[green]Capsule saved:[/] {capsulePath}");
            }
        }
    }
}