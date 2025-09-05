
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

        public async Task<IEnumerable<SearchResult>> RunAsync(string query, int topK, string mode)
        {
            var validator = new CapsuleValidator();
            var validationResult = validator.Validate(capsulePath);
            if (!validationResult.Success)
            {
                throw new Exception(validationResult.Message);
            }

            using (var capsuleManager = new CapsuleManager(capsulePath))
            {
                var provider = await capsuleManager.GetMetaValueAsync("embedding_provider");
                var model = await capsuleManager.GetMetaValueAsync("embedding_model");

                IEmbeddingProvider embeddingProvider;
                if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        throw new Exception("RAGCAP_API_KEY environment variable must be set when using the API provider.");
                    }
                    embeddingProvider = new ApiEmbeddingProvider(model, apiKey);
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
                        break;
                    case "bm25":
                        searcher = new BM25Searcher(capsuleManager);
                        break;
                    case "hybrid":
                    default:
                        searcher = new HybridSearcher(capsuleManager, embeddingProvider);
                        break;
                }

                return await searcher.SearchAsync(query, topK);
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
