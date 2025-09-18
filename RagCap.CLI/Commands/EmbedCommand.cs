using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;
using System;
using RagCap.Core.Utils;

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
            public string? Provider { get; set; }

            [CommandOption("--model")]
            public string? Model { get; set; }

            [CommandOption("--api-key")]
            public string? ApiKey { get; set; }

            [CommandOption("--api-version")]
            public string? ApiVersion { get; set; }

            [CommandOption("--endpoint")]
            public string? Endpoint { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await HandleEmbed(settings);
            return 0;
        }

        private async Task HandleEmbed(Settings settings)
        {
            var config = ConfigManager.GetConfig();

            var provider = settings.Provider ?? Environment.GetEnvironmentVariable("RAGCAP_EMBEDDING_PROVIDER") ?? config.Embedding?.Provider ?? "local";
            var model = settings.Model ?? Environment.GetEnvironmentVariable("RAGCAP_EMBEDDING_MODEL") ?? config.Embedding?.Model;
            var apiVersion = settings.ApiVersion ?? Environment.GetEnvironmentVariable("RAGCAP_API_VERSION") ?? config.Api?.ApiVersion;
            var endpoint = settings.Endpoint ?? Environment.GetEnvironmentVariable("RAGCAP_ENDPOINT") ?? Environment.GetEnvironmentVariable("RAGCAP_AZURE_ENDPOINT") ?? config.Api?.Endpoint;

            using (var capsuleManager = new CapsuleManager(settings.CapsulePath))
            {
                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = settings.ApiKey ?? Environment.GetEnvironmentVariable("RAGCAP_API_KEY") ?? config.Api?.ApiKey;
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        AnsiConsole.MarkupLine("[red]Error: API key must be provided for the API provider via --api-key, RAGCAP_API_KEY environment variable, or config file.[/]");
                        return;
                    }
                    embeddingProvider = new ApiEmbeddingProvider(apiKey, model, endpoint, apiVersion);
                }
                else
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                }

                // Load content
                var loader = RagCap.Core.Ingestion.FileLoaderFactory.GetLoader(settings.SourceFile);
                var content = loader.LoadContent(settings.SourceFile);

                // Create SourceDocument
                var sourceDocument = new RagCap.Core.Capsule.SourceDocument
                {
                    Path = settings.SourceFile,
                    Hash = RagCap.Core.Utils.HashUtils.GetSha256Hash(content),
                    Content = content,
                    DocumentType = System.IO.Path.GetExtension(settings.SourceFile).TrimStart('.').ToLowerInvariant()
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

                AnsiConsole.MarkupLine($"[green]Successfully embedded:[/] {settings.SourceFile}");
            }
        }
    }
}