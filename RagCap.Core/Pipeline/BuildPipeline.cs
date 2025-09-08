using RagCap.Core.Capsule;
using RagCap.Core.Chunking;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public class BuildPipeline
    {
        private readonly CapsuleManager _capsuleManager;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly TokenChunker _tokenChunker;

        public BuildPipeline(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider)
        {
            _capsuleManager = capsuleManager;
            _embeddingProvider = embeddingProvider;
            _tokenChunker = new TokenChunker();
        }

        public async Task RunAsync(string inputPath)
        {
            var files = Directory.GetFiles(inputPath, "*", SearchOption.AllDirectories);
            int sources = 0;
            int chunks = 0;
            int embeddings = 0;

            foreach (var file in files)
            {
                try
                {
                    var loader = FileLoaderFactory.GetLoader(file);
                    var content = loader.LoadContent(file);

                    var sourceDocument = new SourceDocument
                    {
                        Path = file,
                        Hash = HashUtils.GetSha256Hash(content),
                        Content = content
                    };
                    var sourceDocumentId = await _capsuleManager.AddSourceDocumentAsync(sourceDocument);
                    sourceDocument.Id = sourceDocumentId.ToString();
                    sources++;

                    var chunkContent = _tokenChunker.Chunk(sourceDocument);

                    foreach (var chunk in chunkContent)
                    {
                        var chunkId = await _capsuleManager.AddChunkAsync(chunk);
                        chunks++;

                        var embedding = await _embeddingProvider.GenerateEmbeddingAsync(chunk.Content);
                        var embeddingRecord = new Embedding
                        {
                            ChunkId = chunkId.ToString(),
                            Vector = embedding,
                            Dimension = embedding.Length
                        };
                        await _capsuleManager.AddEmbeddingAsync(embeddingRecord);
                        embeddings++;
                    }
                }
                catch (NotSupportedException ex)
                {
                    Console.WriteLine($"Skipping unsupported file type: {file}. {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            }

            Console.WriteLine("\nBuild summary:");
            Console.WriteLine($"  Sources: {sources}");
            Console.WriteLine($"  Chunks: {chunks}");
            Console.WriteLine($"  Embeddings: {embeddings}");
        }
    }
}