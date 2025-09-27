
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public class VectorSearcher : ISearcher
    {
        private readonly CapsuleManager capsuleManager;
        private readonly IEmbeddingProvider embeddingProvider;

        public VectorSearcher(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider)
        {
            this.capsuleManager = capsuleManager;
            this.embeddingProvider = embeddingProvider;
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK, string? includePath = null, string? excludePath = null)
        {
            return await SearchAsyncCandidates(query, topK, null, includePath, excludePath);
        }

        public async Task<IEnumerable<SearchResult>> SearchAsyncCandidates(string query, int topK, IEnumerable<long>? candidateChunkIds, string? includePath = null, string? excludePath = null)
        {
            var queryEmbedding = await embeddingProvider.GenerateEmbeddingAsync(query);
            var results = new List<SearchResult>();

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            SqlFilterUtil.EnsurePathIndex(connection);
            if (candidateChunkIds != null && candidateChunkIds.Any())
            {
                var ids = candidateChunkIds.Distinct().ToList();
                var paramNames = new List<string>(ids.Count);
                for (int i = 0; i < ids.Count; i++)
                {
                    var p = "$id" + i;
                    paramNames.Add(p);
                    command.Parameters.AddWithValue(p, ids[i]);
                }
                command.CommandText = $@"
                SELECT c.id, s.path, c.text, e.vector
                FROM embeddings e
                JOIN chunks c ON c.id = e.chunk_id
                JOIN sources s ON s.id = c.source_id
                WHERE c.id IN ({string.Join(",", paramNames)})";
                var filter = SqlFilterUtil.BuildPathFilterClause(command, includePath, excludePath, "s.path");
                if (!string.IsNullOrEmpty(filter))
                {
                    command.CommandText += " AND " + filter;
                }
            }
            else
            {
                command.CommandText = @"
                SELECT c.id, s.path, c.text, e.vector
                FROM embeddings e
                JOIN chunks c ON c.id = e.chunk_id
                JOIN sources s ON s.id = c.source_id";
                var filter = SqlFilterUtil.BuildPathFilterClause(command, includePath, excludePath, "s.path");
                if (!string.IsNullOrEmpty(filter))
                {
                    command.CommandText += " WHERE " + filter;
                }
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var vector = (byte[])reader.GetValue(3);
                var floatVector = new float[vector.Length / 4];
                Buffer.BlockCopy(vector, 0, floatVector, 0, vector.Length);

                results.Add(new SearchResult
                {
                    ChunkId = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Text = reader.GetString(2),
                    Score = SimilarityMetrics.CosineSimilarity(queryEmbedding, floatVector)
                });
            }

            return results.OrderByDescending(r => r.Score).Take(topK);
        }
    }
}
