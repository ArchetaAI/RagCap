
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
            var capsulePathArgument = new Argument<string>("capsule-path", "The path to the .ragcap file to create.");
            var sourcePathArgument = new Argument<string>("source-path", "The path to the source documents.");

            AddArgument(capsulePathArgument);
            AddArgument(sourcePathArgument);

            this.SetHandler(async (capsulePath, sourcePath) =>
            {
                await HandleBuild(capsulePath, sourcePath);
            }, capsulePathArgument, sourcePathArgument);
        }

        private async Task HandleBuild(string capsulePath, string sourcePath)
        {
            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                capsuleManager.Initialize();

                var embeddingProvider = new LocalEmbeddingProvider();

                var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var loader = FileLoaderFactory.GetLoader(file);
                    var content = await loader.LoadAsync(file);

                    var sourceDocument = new SourceDocument
                    {
                        Path = file,
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
        }

        public static Command Create()
        {
            return new BuildCommand();
        }
    }
}
