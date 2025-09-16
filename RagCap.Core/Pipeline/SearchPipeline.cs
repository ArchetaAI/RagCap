
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Search;
using RagCap.Core.Validation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public class SearchPipeline
    {
        private readonly string capsulePath;

        public SearchPipeline(string capsulePath)
        {
            this.capsulePath = capsulePath;
        }

        public async Task<IEnumerable<SearchResult>> RunAsync(string query, int topK, string mode, int candidateLimit = 500,
            RagCap.Core.Search.VssOptions? vssOptions = null,
            RagCap.Core.Search.VecOptions? vecOptions = null)
        {
            Console.WriteLine("Running SearchPipeline");
            var validator = new CapsuleValidator();
            var validationResult = validator.Validate(capsulePath);
            if (!validationResult.Success)
            {
                throw new Exception(validationResult.Message);
            }

            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                var provider = await capsuleManager.GetMetaValueAsync("embedding_provider") ?? "local";
                var model = await capsuleManager.GetMetaValueAsync("embedding_model");

                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        throw new Exception("RAGCAP_API_KEY environment variable must be set when using the API provider.");
                    }
                    if (string.IsNullOrEmpty(model))
                    {
                        throw new Exception("Embedding model must be specified for API provider.");
                    }
                    var endpoint = await capsuleManager.GetMetaValueAsync("embedding_endpoint");
                    var apiVersion = await capsuleManager.GetMetaValueAsync("embedding_api_version");
                    embeddingProvider = new ApiEmbeddingProvider(apiKey, model, endpoint, apiVersion);
                }
                else
                {
                    embeddingProvider = new LocalEmbeddingProvider();
                }

                ISearcher searcher;
                switch (mode.ToLower())
                {
                    case "vector":
                        searcher = new VectorSearcher(capsuleManager, embeddingProvider);
                        return await searcher.SearchAsync(query, topK);
                    case "bm25":
                        searcher = new BM25Searcher(capsuleManager);
                        return await searcher.SearchAsync(query, topK);
                    case "vss":
                        // Try SQLite VSS; fall back to vector if unavailable
                        try
                        {
                            var vss = new VssVectorSearcher(capsuleManager, embeddingProvider, vssOptions);
                            return await vss.SearchAsync(query, topK);
                        }
                        catch
                        {
                            var vec = new VectorSearcher(capsuleManager, embeddingProvider);
                            return await vec.SearchAsync(query, topK);
                        }
                    case "vec":
                        // sqlite-vec (vec0) module
                        try
                        {
                            var vec = new VecVectorSearcher(capsuleManager, embeddingProvider, vecOptions ?? VecOptions.FromEnvironment());
                            return await vec.SearchAsync(query, topK);
                        }
                        catch
                        {
                            // fallback to hybrid
                            var hybrid2 = new HybridSearcher(capsuleManager, embeddingProvider, candidateLimit);
                            return await hybrid2.SearchAsync(query, topK);
                        }
                    case "hybrid":
                    default:
                        var hybrid = new HybridSearcher(capsuleManager, embeddingProvider, candidateLimit);
                        return await hybrid.SearchAsync(query, topK);
                }
            }
        }
    }

    public class SearchResult
    {
        public int ChunkId { get; set; }
        public string? Source { get; set; }
        public string? Text { get; set; }
        public float Score { get; set; }
    }
}
