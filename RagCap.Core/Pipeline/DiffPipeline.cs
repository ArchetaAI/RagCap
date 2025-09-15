using RagCap.Core.Capsule;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public class DiffPipeline
    {
        private readonly CapsuleManager _capsule1Manager;
        private readonly CapsuleManager _capsule2Manager;

        public DiffPipeline(string capsule1Path, string capsule2Path)
        {
            _capsule1Manager = new CapsuleManager(capsule1Path);
            _capsule2Manager = new CapsuleManager(capsule2Path);
        }

        public async Task<DiffResult> RunAsync()
        {
            var result = new DiffResult();

            await CompareManifests(result);
            await CompareSources(result);
            await CompareChunks(result);
            await CompareEmbeddings(result);
            await CompareRecipes(result);

            return result;
        }

        private async Task CompareManifests(DiffResult result)
        {
            var meta1 = await _capsule1Manager.GetAllMetaAsync();
            var meta2 = await _capsule2Manager.GetAllMetaAsync();

            var allKeys = meta1.Keys.Union(meta2.Keys).ToList();

            foreach (var key in allKeys)
            {
                meta1.TryGetValue(key, out var value1);
                meta2.TryGetValue(key, out var value2);

                if (value1 != value2)
                {
                    result.Manifest[key] = (value1, value2);
                }
            }
        }

        private async Task CompareSources(DiffResult result)
        {
            var sources1 = (await _capsule1Manager.GetAllSourceDocumentsAsync()).ToDictionary(s => s.Path, s => s.Hash);
            var sources2 = (await _capsule2Manager.GetAllSourceDocumentsAsync()).ToDictionary(s => s.Path, s => s.Hash);

            result.AddedSources = sources2.Keys.Except(sources1.Keys).ToList();
            result.RemovedSources = sources1.Keys.Except(sources2.Keys).ToList();

            foreach (var key in sources1.Keys.Intersect(sources2.Keys))
            {
                if (sources1[key] != sources2[key])
                {
                    result.ModifiedSources.Add(key);
                }
            }
        }

        private async Task CompareChunks(DiffResult result)
        {
            var chunks1 = await _capsule1Manager.GetAllChunksAsync();
            var chunks2 = await _capsule2Manager.GetAllChunksAsync();

            result.ChunkCount = (chunks1.Count(), chunks2.Count());

            if (chunks1.Any())
                result.AverageChunkSize = (chunks1.Average(c => c.Content?.Length ?? 0), result.AverageChunkSize.Item2);
            if (chunks2.Any())
                result.AverageChunkSize = (result.AverageChunkSize.Item1, chunks2.Average(c => c.Content?.Length ?? 0));
        }

        private async Task CompareEmbeddings(DiffResult result)
        {
            var embedding1 = await _capsule1Manager.GetEmbeddingAsync("1");
            var embedding2 = await _capsule2Manager.GetEmbeddingAsync("1");

            var dim1 = embedding1?.Dimension ?? 0;
            var dim2 = embedding2?.Dimension ?? 0;

            result.EmbeddingDimensions = (dim1, dim2);
        }

        private async Task CompareRecipes(DiffResult result)
        {
            var recipe1 = await _capsule1Manager.GetMetaValueAsync("recipe");
            var recipe2 = await _capsule2Manager.GetMetaValueAsync("recipe");

            if (recipe1 != recipe2)
            {
                result.Recipe = (recipe1, recipe2);
            }
        }
    }
}
