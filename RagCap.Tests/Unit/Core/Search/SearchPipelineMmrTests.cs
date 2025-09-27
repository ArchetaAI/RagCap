using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using Xunit;

namespace RagCap.Tests.Unit.Core.Search
{
    public class SearchPipelineMmrTests
    {
        private sealed class StubEmbeddingProvider : IEmbeddingProvider
        {
            private readonly float[] vec;
            public StubEmbeddingProvider(float[] v) { vec = v; }
            public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
                => Task.FromResult(vec);
        }

        [Fact]
        public async Task Vector_MMR_Produces_RerankScores_And_JSON_Contains_Fields()
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"ragcap_test_{Guid.NewGuid():N}.ragcap");
            try
            {
                using (var cap = new CapsuleManager(tmp))
                {
                    // Two sources
                    var s1 = await cap.AddSourceDocumentAsync(new SourceDocument { Path = "a/doc1.txt", Hash = "h1" });
                    var s2 = await cap.AddSourceDocumentAsync(new SourceDocument { Path = "b/doc2.txt", Hash = "h2" });

                    // Four chunks
                    var c1 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s1.ToString(), Content = "alpha one", TokenCount = 2 });
                    var c2 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s1.ToString(), Content = "alpha two", TokenCount = 2 });
                    var c3 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s2.ToString(), Content = "beta one", TokenCount = 2 });
                    var c4 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s2.ToString(), Content = "beta two", TokenCount = 2 });

                    // Embeddings in R^3
                    await cap.AddEmbeddingAsync(new Embedding { ChunkId = c1.ToString(), Vector = new float[] { 1f, 0f, 0f }, Dimension = 3 });
                    await cap.AddEmbeddingAsync(new Embedding { ChunkId = c2.ToString(), Vector = new float[] { 0.9f, 0.1f, 0f }, Dimension = 3 });
                    await cap.AddEmbeddingAsync(new Embedding { ChunkId = c3.ToString(), Vector = new float[] { 0f, 1f, 0f }, Dimension = 3 });
                    await cap.AddEmbeddingAsync(new Embedding { ChunkId = c4.ToString(), Vector = new float[] { 0f, 0.9f, 0.1f }, Dimension = 3 });
                }

                // Query aligned with e1
                var provider = new StubEmbeddingProvider(new float[] { 1f, 0f, 0f });
                var pipeline = new SearchPipeline(tmp, provider);

                var res = await pipeline.RunAsync("ignored", 3, "vector", includePath: null, excludePath: null,
                    mmr: true, mmrLambda: 0.5f, mmrPool: 4, searchPool: 0, scoreMode: "mmr");

                var list = res.ToList();
                Assert.True(list.Count <= 3 && list.Count >= 2);
                Assert.All(list, r => Assert.True(r.RetrievalScore.HasValue));
                Assert.All(list, r => Assert.True(r.RerankScore.HasValue));

                // JSON shape includes the extra fields
                var json = JsonSerializer.Serialize(list);
                Assert.Contains("RetrievalScore", json);
                Assert.Contains("RerankScore", json);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}

