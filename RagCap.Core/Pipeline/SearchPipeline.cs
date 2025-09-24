using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Search;
using RagCap.Core.Validation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RagCap.Core.Utils;
using System.Text.RegularExpressions;
using System.Linq;

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
            RagCap.Core.Search.VecOptions? vecOptions = null,
            string? includePath = null,
            string? excludePath = null,
            bool mmr = false,
            float mmrLambda = 0.5f,
            int mmrPool = 50)
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
                    var config = ConfigManager.GetConfig();
                    var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY") ?? config.Api?.ApiKey;
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        throw new Exception("RAGCAP_API_KEY environment variable or config file entry must be set when using the API provider.");
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
                        {
                            var res = await searcher.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            if (mmr)
                            {
                                res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                            }
                            return res;
                        }
                    case "bm25":
                        searcher = new BM25Searcher(capsuleManager);
                        {
                            var res = await searcher.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            return res;
                        }
                    case "vss":
                        // Try SQLite VSS; fall back to vector if unavailable
                        try
                        {
                            var vss = new VssVectorSearcher(capsuleManager, embeddingProvider, vssOptions);
                            var res = await vss.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            if (mmr)
                            {
                                res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                            }
                            return res;
                        }
                        catch
                        {
                            var vec = new VectorSearcher(capsuleManager, embeddingProvider);
                            var res = await vec.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            if (mmr)
                            {
                                res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                            }
                            return res;
                        }
                    case "vec":
                        // sqlite-vec (vec0) module
                        try
                        {
                            var vec = new VecVectorSearcher(capsuleManager, embeddingProvider, vecOptions ?? VecOptions.FromEnvironment());
                            var res = await vec.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            if (mmr)
                            {
                                res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                            }
                            return res;
                        }
                        catch
                        {
                            // fallback to hybrid
                            var hybrid2 = new HybridSearcher(capsuleManager, embeddingProvider, candidateLimit);
                            {
                                var res = await hybrid2.SearchAsync(query, topK);
                                res = FilterByPath(res, includePath, excludePath);
                                if (mmr)
                                {
                                    res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                                }
                                return res;
                            }
                        }
                    case "hybrid":
                    default:
                        var hybrid = new HybridSearcher(capsuleManager, embeddingProvider, candidateLimit);
                        {
                            var res = await hybrid.SearchAsync(query, topK);
                            res = FilterByPath(res, includePath, excludePath);
                            if (mmr)
                            {
                                res = await ApplyMmrAsync(res, capsuleManager, embeddingProvider, query, topK, mmrPool, mmrLambda);
                            }
                            return res;
                        }
                }
            }
        }

        private static IEnumerable<SearchResult> FilterByPath(IEnumerable<SearchResult> results, string? include, string? exclude)
        {
            var list = results.ToList();
            if (string.IsNullOrWhiteSpace(include) && string.IsNullOrWhiteSpace(exclude)) return list;

            var inc = BuildGlobRegexes(include);
            var exc = BuildGlobRegexes(exclude);

            bool Matches(string path)
            {
                var p = path ?? string.Empty;
                if (exc.Any(rx => rx.IsMatch(p))) return false;
                if (inc.Count == 0) return true;
                return inc.Any(rx => rx.IsMatch(p));
            }

            return list.Where(r => Matches(r.Source ?? string.Empty));
        }

        private static List<Regex> BuildGlobRegexes(string? patterns)
        {
            var list = new List<Regex>();
            if (string.IsNullOrWhiteSpace(patterns)) return list;
            var parts = patterns.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var rx = Regex.Escape(part)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".");
                list.Add(new Regex("^" + rx + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }
            return list;
        }

        private static async Task<IEnumerable<SearchResult>> ApplyMmrAsync(IEnumerable<SearchResult> results,
            CapsuleManager capsuleManager,
            Embeddings.IEmbeddingProvider embeddingProvider,
            string query,
            int topK,
            int poolSize,
            float lambda)
        {
            var list = results.ToList();
            if (list.Count <= 1 || topK <= 1) return list.Take(topK);

            var pool = list.Take(Math.Min(poolSize, list.Count)).ToList();
            var q = await embeddingProvider.GenerateEmbeddingAsync(query);

            var vectors = await LoadVectorsAsync(capsuleManager, pool.Select(r => r.ChunkId));

            float CosSim(float[] a, float[] b)
            {
                double dot = 0, na = 0, nb = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    var x = a[i]; var y = b[i];
                    dot += x * y; na += x * x; nb += y * y;
                }
                return (float)(dot / (1e-9 + Math.Sqrt(na) * Math.Sqrt(nb)));
            }

            var qsim = new Dictionary<int, float>();
            foreach (var r in pool)
            {
                if (vectors.TryGetValue(r.ChunkId, out var v))
                {
                    qsim[r.ChunkId] = CosSim(q, v);
                }
                else
                {
                    qsim[r.ChunkId] = 0f;
                }
            }

            var selected = new List<SearchResult>();
            var selectedIds = new HashSet<int>();

            // Seed with best by query similarity
            var first = pool.OrderByDescending(r => qsim[r.ChunkId]).First();
            selected.Add(first);
            selectedIds.Add(first.ChunkId);

            while (selected.Count < Math.Min(topK, pool.Count))
            {
                SearchResult? best = null;
                float bestScore = float.NegativeInfinity;
                foreach (var cand in pool)
                {
                    if (selectedIds.Contains(cand.ChunkId)) continue;
                    var rel = qsim[cand.ChunkId];
                    float maxDiv = 0f;
                    if (vectors.TryGetValue(cand.ChunkId, out var vCand))
                    {
                        foreach (var s in selected)
                        {
                            if (vectors.TryGetValue(s.ChunkId, out var vSel))
                            {
                                var sim = CosSim(vCand, vSel);
                                if (sim > maxDiv) maxDiv = sim;
                            }
                        }
                    }
                    var mmrScore = lambda * rel - (1 - lambda) * maxDiv;
                    if (mmrScore > bestScore)
                    {
                        bestScore = mmrScore;
                        best = cand;
                    }
                }
                if (best == null) break;
                selected.Add(best);
                selectedIds.Add(best.ChunkId);
            }

            return selected;
        }

        private static async Task<Dictionary<int, float[]>> LoadVectorsAsync(CapsuleManager capsuleManager, IEnumerable<int> chunkIds)
        {
            var idList = chunkIds.Distinct().ToList();
            var dict = new Dictionary<int, float[]>(idList.Count);
            if (idList.Count == 0) return dict;
            using var conn = capsuleManager.Connection;
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            var paramNames = new List<string>(idList.Count);
            for (int i = 0; i < idList.Count; i++)
            {
                var p = "$id" + i;
                paramNames.Add(p);
                cmd.Parameters.AddWithValue(p, idList[i]);
            }
            cmd.CommandText = $"SELECT chunk_id, vector FROM embeddings WHERE chunk_id IN ({string.Join(",", paramNames)});";
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var id = rdr.GetInt32(0);
                var blob = (byte[])rdr.GetValue(1);
                var vec = new float[blob.Length / 4];
                Buffer.BlockCopy(blob, 0, vec, 0, blob.Length);
                dict[id] = vec;
            }
            return dict;
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
