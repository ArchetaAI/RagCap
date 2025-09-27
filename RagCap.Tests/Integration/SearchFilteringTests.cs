using System;
using System.IO;
using System.Threading.Tasks;
using RagCap.Core.Capsule;
using RagCap.Core.Pipeline;
using RagCap.Core.Search;
using Xunit;

namespace RagCap.Tests.Integration
{
    public class SearchFilteringTests
    {
        [Fact]
        public async Task Bm25_Search_Respects_Include_Exclude_Paths()
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"ragcap_test_{Guid.NewGuid():N}.ragcap");
            try
            {
                using (var cap = new CapsuleManager(tmp))
                {
                    // Add sources
                    var s1 = await cap.AddSourceDocumentAsync(new SourceDocument { Path = "docs/a.txt", Hash = "h1" });
                    var s2 = await cap.AddSourceDocumentAsync(new SourceDocument { Path = "lib/b.txt", Hash = "h2" });

                    // Add chunks
                    var c1 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s1.ToString(), Content = "alpha text", TokenCount = 2 });
                    var c2 = await cap.AddChunkAsync(new Chunk { SourceDocumentId = s2.ToString(), Content = "beta text", TokenCount = 2 });
                }

                var pipeline = new SearchPipeline(tmp);

                // Include only docs/** -> should return only alpha text
                var resInc = await pipeline.RunAsync("text", topK: 5, mode: "bm25", includePath: "docs/**", excludePath: null, mmr: false, mmrLambda: 0.5f, mmrPool: 50);
                var listInc = System.Linq.Enumerable.ToList(resInc);
                Assert.Single(listInc);
                Assert.Contains("docs/", listInc[0].Source);

                // Exclude docs/** -> should return only beta text
                var resExc = await pipeline.RunAsync("text", topK: 5, mode: "bm25", includePath: null, excludePath: "docs/**", mmr: false, mmrLambda: 0.5f, mmrPool: 50);
                var listExc = System.Linq.Enumerable.ToList(resExc);
                Assert.Single(listExc);
                Assert.Contains("lib/", listExc[0].Source);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}

