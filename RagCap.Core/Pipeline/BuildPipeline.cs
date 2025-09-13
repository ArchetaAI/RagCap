using RagCap.Core.Capsule;
using RagCap.Core.Chunking;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using RagCap.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public class BuildPipeline : IBuildPipeline
    {
        private readonly CapsuleManager _capsuleManager;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly TokenChunker _tokenChunker;
        private readonly Preprocessor _preprocessor;

        public BuildPipeline(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider, Recipe recipe = null)
        {
            _capsuleManager = capsuleManager;
            _embeddingProvider = embeddingProvider;

            var chunkSize = recipe?.Chunking?.Size ?? 500;
            var overlap = recipe?.Chunking?.Overlap ?? 50;
            _tokenChunker = new TokenChunker(chunkSize, overlap);

            var boilerplate = recipe?.Preprocess?.Boilerplate ?? true;
            var preserveCode = recipe?.Preprocess?.Preserve_code ?? true;
            var flattenTables = recipe?.Preprocess?.Flatten_tables ?? true;
            var detectLanguage = recipe?.Preprocess?.Detect_language ?? true;
            _preprocessor = new Preprocessor(boilerplate, preserveCode, flattenTables, detectLanguage);
        }

        public async Task RunAsync(string inputPath, List<string> sourcesFromRecipe = null)
        {
            var files = new List<string>();
            if (sourcesFromRecipe != null && sourcesFromRecipe.Count > 0)
            {
                foreach (var source in sourcesFromRecipe)
                {
                    if (Directory.Exists(source))
                    {
                        files.AddRange(Directory.GetFiles(source, "*", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(source))
                    {
                        files.Add(source);
                    }
                }
            }
            else
            {
                if (Directory.Exists(inputPath))
                {
                    files.AddRange(Directory.GetFiles(inputPath, "*", SearchOption.AllDirectories));
                }
                else if (File.Exists(inputPath))
                {
                    files.Add(inputPath);
                }
            }

            int sources = 0;
            int chunks = 0;
            int embeddings = 0;

            foreach (var file in files)
            {
                try
                {
                    var loader = FileLoaderFactory.GetLoader(file);
                    var content = loader.LoadContent(file);
                    content = _preprocessor.Process(content);

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