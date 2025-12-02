using RagCap.Core.Capsule;
using RagCap.Core.Chunking;
using RagCap.Core.Embeddings;
using RagCap.Core.Ingestion;
using RagCap.Core.Processing;
using RagCap.Core.Recipes;
using RagCap.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.Core.Pipeline
{
    public class BuildPipeline : IBuildPipeline
    {
        private readonly CapsuleManager _capsuleManager;
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly object _chunker; // TokenChunker or BertTokenChunker
        private readonly Preprocessor _preprocessor;

        public BuildPipeline(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider, Recipe? recipe = null)
        {
            _capsuleManager = capsuleManager;
            _embeddingProvider = embeddingProvider;

            var chunkSize = recipe?.Chunking?.Size ?? 200;
            var overlap = recipe?.Chunking?.Overlap ?? 50;
            var useBert = recipe?.Chunking?.BertAware ?? true;
            if (useBert)
            {
                try
                {
                    _chunker = new RagCap.Core.Chunking.BertTokenChunker(chunkSize, overlap);
                }
                catch (Exception)
                {
                    // Fallback to simple chunker if BERT vocab not available
                    _chunker = new TokenChunker(chunkSize, overlap);
                }
            }
            else
            {
                _chunker = new TokenChunker(chunkSize, overlap);
            }

            var boilerplate = recipe?.Preprocess?.Boilerplate ?? true;
            var preserveCode = recipe?.Preprocess?.PreserveCode ?? true;
            var flattenTables = recipe?.Preprocess?.FlattenTables ?? true;
            var detectLanguage = recipe?.Preprocess?.DetectLanguage ?? true;
            _preprocessor = new Preprocessor(boilerplate, preserveCode, flattenTables, detectLanguage);

            // Configure HTML extraction options if provided via recipe
            try
            {
                RagCap.Core.Ingestion.HtmlFileLoader.Options = new RagCap.Core.Ingestion.HtmlFileLoader.HtmlExtractionOptions
                {
                    IncludeHeadingContext = recipe?.Preprocess?.IncludeHeadingContext ?? true,
                    IncludeTitle = true
                };
            }
            catch { }
        }

        public async Task RunAsync(string inputPath, List<string>? sourcesFromRecipe = null)
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

            // Degree of parallelism for embedding generation (defaults to CPU count, cap to 8)
            int dop = Math.Min(8, Math.Max(1, BuildPipelineEnv.EnvInt("RAGCAP_BUILD_DOP", Environment.ProcessorCount)));

            // Normalize and filter files: supported extensions only, skip hidden
            var supported = new HashSet<string>(new[] { ".txt", ".md", ".pdf", ".html", ".csv" }, StringComparer.OrdinalIgnoreCase);
            var filtered = new List<string>();
            foreach (var f in files)
            {
                try
                {
                    var ext = Path.GetExtension(f);
                    if (!supported.Contains(ext)) continue;
                    var attr = File.GetAttributes(f);
                    if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                    filtered.Add(f);
                }
                catch { /* ignore */ }
            }

            RagCap.Core.Utils.Logging.Debug($"Discovered {filtered.Count} files after filtering");

            await AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Building capsule...[/]", new ProgressTaskSettings
                    {
                        MaxValue = filtered.Count
                    });

                    foreach (var file in filtered)
                    {
                        task.Description = $"[green]Processing:[/] {Path.GetFileName(file)}";
                        try
                        {
                            var loader = FileLoaderFactory.GetLoader(file);
                            var content = loader.LoadContent(file);

                            var sourceDocument = new SourceDocument
                            {
                                Path = file,
                                Hash = HashUtils.GetSha256Hash(content),
                                Content = content,
                                DocumentType = Path.GetExtension(file).TrimStart('.').ToLowerInvariant()
                            };

                            sourceDocument.Content = _preprocessor.Process(sourceDocument);

                            // Use a transaction to insert source and chunks efficiently
                            List<Chunk> chunkContent;
                            using (var tx = _capsuleManager.BeginTransaction())
                            {
                                var sourceDocumentId = await _capsuleManager.AddSourceDocumentAsync(sourceDocument, tx);
                                sourceDocument.Id = sourceDocumentId.ToString();
                                sources++;

                                // Now that sourceDocument.Id is set, we can chunk
                                chunkContent = (_chunker is RagCap.Core.Chunking.BertTokenChunker b)
                                    ? b.Chunk(sourceDocument)
                                    : ((TokenChunker)_chunker).Chunk(sourceDocument);

                                // Insert chunks and capture IDs
                                foreach (var chunk in chunkContent)
                                {
                                    var chunkId = await _capsuleManager.AddChunkAsync(chunk, tx);
                                    chunk.Id = chunkId;
                                    chunks++;
                                }
                                tx.Commit();
                            }

                            // Compute embeddings in parallel (bounded)
                            var vectors = new Dictionary<long, float[]>(capacity: chunkContent.Count);
                            var throttler = new System.Threading.SemaphoreSlim(dop);
                            var tasks = new List<Task>();
                            foreach (var chunk in chunkContent)
                            {
                                await throttler.WaitAsync();
                                tasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        var v = await _embeddingProvider.GenerateEmbeddingAsync(chunk.Content ?? string.Empty);
                                        lock (vectors)
                                        {
                                            vectors[chunk.Id] = v;
                                        }
                                    }
                                    finally
                                    {
                                        throttler.Release();
                                    }
                                }));
                            }
                            await Task.WhenAll(tasks);

                            // Insert embeddings in a second transaction
                            using (var tx = _capsuleManager.BeginTransaction())
                            {
                                foreach (var kv in vectors)
                                {
                                    var embeddingRecord = new Embedding
                                    {
                                        ChunkId = kv.Key.ToString(),
                                        Vector = kv.Value,
                                        Dimension = kv.Value.Length
                                    };
                                    await _capsuleManager.AddEmbeddingAsync(embeddingRecord, tx);
                                    embeddings++;
                                }
                                tx.Commit();
                            }
                        }
                        catch (NotSupportedException ex)
                        {
                            RagCap.Core.Utils.Logging.Debug($"Skipping unsupported file type: {file}. {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            RagCap.Core.Utils.Logging.Error($"Error processing file {file}: {ex.Message}");
                        }
                        finally
                        {
                            task.Increment(1);
                        }
                    }
                });

            Console.WriteLine("\nBuild summary:");
            Console.WriteLine($"  Sources: {sources}");
            Console.WriteLine($"  Chunks: {chunks}");
            Console.WriteLine($"  Embeddings: {embeddings}");
        }
    }
}

// Local helpers for BuildPipeline
namespace RagCap.Core.Pipeline
{
    internal static class BuildPipelineEnv
    {
        public static int EnvInt(string name, int fallback)
        {
            var s = System.Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            return int.TryParse(s, out var v) ? v : fallback;
        }
    }
}
