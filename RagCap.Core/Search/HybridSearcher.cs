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
        private readonly int candidateLimit;

        public HybridSearcher(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider, int candidateLimit = 500)
        {
            this.vectorSearcher = new VectorSearcher(capsuleManager, embeddingProvider);
            this.bm25Searcher = new BM25Searcher(capsuleManager);
            this.candidateLimit = candidateLimit;
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK)
        {
            // Use BM25 to select a candidate set and vector to re-rank for scalability
            var candidateIds = await bm25Searcher.SearchChunkIdsAsync(query, candidateLimit);

            IEnumerable<SearchResult> vectorResults;
            if (candidateIds.Count > 0)
            {
                vectorResults = await vectorSearcher.SearchAsyncCandidates(query, topK, candidateIds);
            }
            else
            {
                // Fallback: no BM25 hits, scan full vector space
                vectorResults = await vectorSearcher.SearchAsync(query, topK);
            }

            var bm25Results = await bm25Searcher.SearchAsync(query, topK);

            // Materialize lists to avoid repeated enumeration and index lookups
            var bm25List = bm25Results.ToList();
            var vectorList = vectorResults.ToList();

            // Precompute ranks for RRF
            var bm25Rank = new Dictionary<int, int>(bm25List.Count);
            for (int i = 0; i < bm25List.Count; i++) bm25Rank[bm25List[i].ChunkId] = i;
            var vecRank = new Dictionary<int, int>(vectorList.Count);
            for (int i = 0; i < vectorList.Count; i++) vecRank[vectorList[i].ChunkId] = i;

            var combinedResults = new Dictionary<int, SearchResult>();

            // Combine BM25 with RRF (k=60)
            const float k = 60f;
            foreach (var r in bm25List)
            {
                var score = 1.0f / (k + bm25Rank[r.ChunkId] + 1);
                combinedResults[r.ChunkId] = new SearchResult
                {
                    ChunkId = r.ChunkId,
                    Source = r.Source,
                    Text = r.Text,
                    Score = score
                };
            }

            // Merge vector results
            foreach (var r in vectorList)
            {
                var score = 1.0f / (k + vecRank[r.ChunkId] + 1);
                if (combinedResults.TryGetValue(r.ChunkId, out var existing))
                {
                    existing.Score += score;
                }
                else
                {
                    combinedResults[r.ChunkId] = new SearchResult
                    {
                        ChunkId = r.ChunkId,
                        Source = r.Source,
                        Text = r.Text,
                        Score = score
                    };
                }
            }

            return combinedResults.Values
                .OrderByDescending(r => r.Score)
                .Take(topK);
        }
    }
}
