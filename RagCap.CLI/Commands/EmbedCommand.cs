
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
            public required string CapsulePath { get; set; }

            [CommandArgument(1, "<source-file>")]
            public required string SourceFile { get; set; }

            [CommandOption("--provider")]
            [DefaultValue("local")]
            public string Provider { get; set; } = "local";

            [CommandOption("--api-provider")]
            [DefaultValue("openai")]
            public string ApiProvider { get; set; } = "openai";

            [CommandOption("--api-key")]
            public string? ApiKey { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleEmbed(settings.CapsulePath, settings.SourceFile, settings.Provider, settings.ApiProvider, settings.ApiKey);
            return 0;
        }

        private async Task HandleEmbed(string capsulePath, string sourceFile, string provider, string? apiProvider, string? apiKey)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        AnsiConsole.MarkupLine("[red]Error: API key must be provided for the API provider via --api-key or RAGCAP_API_KEY environment variable.[/]");
                        return;
                    }
                    if (string.IsNullOrEmpty(apiProvider))
                    {
                        AnsiConsole.MarkupLine("[red]Error: API provider name must be specified via --api-provider.[/]");
                        return;
                    }
                    embeddingProvider = new ApiEmbeddingProvider(apiProvider!, apiKey!);
                }
                else
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                    await Task.CompletedTask; // To satisfy async method warning
                }

                // Load content
                var loader = RagCap.Core.Ingestion.FileLoaderFactory.GetLoader(sourceFile);
                var content = loader.LoadContent(sourceFile);

                // Create SourceDocument
                var sourceDocument = new RagCap.Core.Capsule.SourceDocument
                {
                    Path = sourceFile,
                    Hash = RagCap.Core.Utils.HashUtils.GetSha256Hash(content),
                    Content = content,
                    DocumentType = System.IO.Path.GetExtension(sourceFile).TrimStart('.').ToLowerInvariant()
                };

                // Add SourceDocument to capsule
                var sourceDocumentId = await capsuleManager.AddSourceDocumentAsync(sourceDocument);
                sourceDocument.Id = sourceDocumentId.ToString();

                // Chunk content
                var tokenChunker = new RagCap.Core.Chunking.TokenChunker(); // Use default chunking for now
                var chunkContent = tokenChunker.Chunk(sourceDocument);

                // Generate and add embeddings for each chunk
                foreach (var chunk in chunkContent)
                {
                    var chunkId = await capsuleManager.AddChunkAsync(chunk);
                    chunk.Id = chunkId; // Update chunk ID

                    var embedding = await embeddingProvider.GenerateEmbeddingAsync(chunk.Content ?? string.Empty);
                    var embeddingRecord = new RagCap.Core.Capsule.Embedding
                    {
                        ChunkId = chunk.Id.ToString(),
                        Vector = embedding,
                        Dimension = embedding.Length
                    };
                    await capsuleManager.AddEmbeddingAsync(embeddingRecord);
                }

                AnsiConsole.MarkupLine($"[green]Successfully embedded:[/] {sourceFile}");
            }
        }
    }
}
