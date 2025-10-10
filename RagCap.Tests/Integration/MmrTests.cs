using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using Xunit;
using RagCap.Tests.TestUtils;

namespace RagCap.Tests.Integration
{
    public class MmrTests
    {
        [Fact]
        public async Task Mmr_Reranks_Results_With_Build()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"ragcap_test_{Guid.NewGuid():N}");
            var capsulePath = Path.Combine(tempDir, "test.ragcap");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create dummy files
                File.WriteAllText(Path.Combine(tempDir, "doc1.txt"), "This document is about machine learning.");
                File.WriteAllText(Path.Combine(tempDir, "doc2.txt"), "This document is about web development.");
                File.WriteAllText(Path.Combine(tempDir, "doc3.txt"), "This document is about cooking.");

                // Build the capsule
                var capsuleManager = new CapsuleManager(capsulePath);
                var embeddingProvider = new FakeEmbeddingProvider();
                var buildPipeline = new BuildPipeline(capsuleManager, embeddingProvider);
                await buildPipeline.RunAsync(tempDir);

                // Search the capsule
                var searchPipeline = new SearchPipeline(capsulePath, new FakeEmbeddingProvider());

                // Run search without MMR
                var resultsNoMmr = await searchPipeline.RunAsync("machine learning", topK: 3, mode: "vector", mmr: false);
                var listNoMmr = resultsNoMmr.ToList();

                // Run search with MMR
                var resultsMmr = await searchPipeline.RunAsync("machine learning", topK: 3, mode: "vector", mmr: true, mmrLambda: 0.2f, mmrPool: 10);
                var listMmr = resultsMmr.ToList();

                // Assertions
                Assert.Equal(3, listNoMmr.Count);
                Assert.Equal(3, listMmr.Count);

                // With MMR, the order should be different
                Assert.NotEqual(listNoMmr[0].Source, listMmr[0].Source);

                // Scores should be different
                Assert.NotEqual(listNoMmr[0].Score, listMmr[0].Score);

                // RerankScore should be present with MMR
                Assert.All(listMmr, item => Assert.True(item.RerankScore.HasValue));
                Assert.All(listMmr, item => Assert.True(item.RetrievalScore.HasValue));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task Mmr_Reranks_Results_With_Bm25()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"ragcap_test_{Guid.NewGuid():N}");
            var capsulePath = Path.Combine(tempDir, "test.ragcap");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create dummy files
                File.WriteAllText(Path.Combine(tempDir, "doc1.txt"), "This document is about machine learning.");
                File.WriteAllText(Path.Combine(tempDir, "doc2.txt"), "This document is about web development.");
                File.WriteAllText(Path.Combine(tempDir, "doc3.txt"), "This document is about cooking.");

                // Build the capsule
                var capsuleManager = new CapsuleManager(capsulePath);
                var embeddingProvider = new FakeEmbeddingProvider();
                var buildPipeline = new BuildPipeline(capsuleManager, embeddingProvider);
                await buildPipeline.RunAsync(tempDir);

                // Search the capsule
                var searchPipeline = new SearchPipeline(capsulePath, new FakeEmbeddingProvider());

                // Run search without MMR
                var resultsNoMmr = await searchPipeline.RunAsync("machine learning", topK: 3, mode: "bm25", mmr: false);
                var listNoMmr = resultsNoMmr.ToList();

                // Run search with MMR
                var resultsMmr = await searchPipeline.RunAsync("machine learning", topK: 3, mode: "bm25", mmr: true, mmrLambda: 0.2f, mmrPool: 10);
                var listMmr = resultsMmr.ToList();

                // Assertions
                Assert.Equal(3, listNoMmr.Count);
                Assert.Equal(3, listMmr.Count);

                // With MMR, the order should be different
                Assert.NotEqual(listNoMmr[0].Source, listMmr[0].Source);

                // Scores should be different
                Assert.NotEqual(listNoMmr[0].Score, listMmr[0].Score);

                // RerankScore should be present with MMR
                Assert.All(listMmr, item => Assert.True(item.RerankScore.HasValue));
                Assert.All(listMmr, item => Assert.True(item.RetrievalScore.HasValue));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Theory]
        [InlineData("original")]
        [InlineData("mmr")]
        [InlineData("retrieval")]
        public async Task ScoreMode_Returns_Correct_Score(string scoreMode)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"ragcap_test_{Guid.NewGuid():N}");
            var capsulePath = Path.Combine(tempDir, "test.ragcap");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create dummy files
                File.WriteAllText(Path.Combine(tempDir, "doc1.txt"), "This document is about machine learning.");
                File.WriteAllText(Path.Combine(tempDir, "doc2.txt"), "This document is about web development.");
                File.WriteAllText(Path.Combine(tempDir, "doc3.txt"), "This document is about cooking.");

                // Build the capsule
                var capsuleManager = new CapsuleManager(capsulePath);
                var embeddingProvider = new FakeEmbeddingProvider();
                var buildPipeline = new BuildPipeline(capsuleManager, embeddingProvider);
                await buildPipeline.RunAsync(tempDir);

                // Search the capsule
                var searchPipeline = new SearchPipeline(capsulePath, new FakeEmbeddingProvider());

                // Run search with MMR
                var results = await searchPipeline.RunAsync("machine learning", topK: 3, mode: "vector", mmr: true, mmrLambda: 0.2f, mmrPool: 10, scoreMode: scoreMode);
                var list = results.ToList();

                // Assertions
                Assert.NotEmpty(list);
                foreach (var item in list)
                {
                    switch (scoreMode)
                    {
                        case "mmr":
                            Assert.Equal(item.RerankScore, item.Score);
                            break;
                        case "retrieval":
                            Assert.Equal(item.RetrievalScore, item.Score);
                            break;
                        case "original":
                            Assert.Equal(item.RetrievalScore, item.Score);
                            break;
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
