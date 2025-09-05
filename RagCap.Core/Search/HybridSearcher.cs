using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public class HybridSearcher : ISearcher
    {
        private readonly VectorSearcher vectorSearcher;
        private readonly BM25Searcher bm25Searcher;

        public HybridSearcher(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider)
        {
            this.vectorSearcher = new VectorSearcher(capsuleManager, embeddingProvider);
            this.bm25Searcher = new BM25Searcher(capsuleManager);
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK)
        {
            var vectorResults = await vectorSearcher.SearchAsync(query, topK);
            var bm25Results = await bm25Searcher.SearchAsync(query, topK);

            var combinedResults = new Dictionary<int, SearchResult>();

            // Normalize and combine BM25 scores
            var bm25Lookup = bm25Results.ToDictionary(r => r.ChunkId);
            foreach (var result in bm25Results)
            {
                combinedResults[result.ChunkId] = new SearchResult
                {
                    ChunkId = result.ChunkId,
                    Source = result.Source,
                    Text = result.Text,
                    Score = 1.0f / (60 + bm25Results.ToList().IndexOf(result) + 1) // Reciprocal Rank Fusion
                };
            }

            // Normalize and combine vector scores
            foreach (var result in vectorResults)
            {
                if (combinedResults.TryGetValue(result.ChunkId, out var existingResult))
                {
                    existingResult.Score += 1.0f / (60 + vectorResults.ToList().IndexOf(result) + 1); // Reciprocal Rank Fusion
                }
                else
                {
                    combinedResults[result.ChunkId] = new SearchResult
                    {
                        ChunkId = result.ChunkId,
                        Source = result.Source,
                        Text = result.Text,
                        Score = 1.0f / (60 + vectorResults.ToList().IndexOf(result) + 1) // Reciprocal Rank Fusion
                    };
                }
            }

            return combinedResults.Values.OrderByDescending(r => r.Score).Take(topK);
        }
    }
}
