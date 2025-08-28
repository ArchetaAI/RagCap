
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class EmbedCommand : Command
    {
        public EmbedCommand() : base("embed", "Generate embeddings for a single file and add them to a capsule.")
        {
            var capsulePathArgument = new Argument<string>("capsule-path", "The path to the .ragcap file.");
            var sourceFileArgument = new Argument<string>("source-file", "The path to the source file.");

            AddArgument(capsulePathArgument);
            AddArgument(sourceFileArgument);

            this.SetHandler(async (capsulePath, sourceFile) =>
            {
                await HandleEmbed(capsulePath, sourceFile);
            }, capsulePathArgument, sourceFileArgument);
        }

        private async Task HandleEmbed(string capsulePath, string sourceFile)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                var embeddingProvider = new LocalEmbeddingProvider();

                var loader = FileLoaderFactory.GetLoader(sourceFile);
                var content = await loader.LoadAsync(sourceFile);

                var sourceDocument = new SourceDocument
                {
                    Path = sourceFile,
                    Hash = ""
                };
                var sourceDocumentId = await capsuleManager.AddSourceDocumentAsync(sourceDocument);

                var chunks = Chunker.ChunkText(content, 1000, 100);

                foreach (var chunk in chunks)
                {
                    chunk.SourceDocumentId = sourceDocumentId.ToString();
                    var chunkId = await capsuleManager.AddChunkAsync(chunk);

                    var embedding = await embeddingProvider.GenerateEmbeddingAsync(chunk.Content);
                    var embeddingRecord = new Embedding
                    {
                        ChunkId = chunkId.ToString(),
                        Vector = embedding,
                        Dimension = embedding.Length
                    };
                    await capsuleManager.AddEmbeddingAsync(embeddingRecord);
                }
            }
        }

        public static Command Create()
        {
            return new EmbedCommand();
        }
    }
}
