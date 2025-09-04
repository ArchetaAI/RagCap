
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public class VectorSearcher
    {
        private readonly SqliteConnection _connection;

        public VectorSearcher(SqliteConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<(long chunk_id, float score)>> SearchAsync(float[] queryEmbedding, int topK)
        {
            var results = new List<(long chunk_id, float score)>();

            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT chunk_id, vector FROM embeddings";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var chunkId = reader.GetInt64(0);
                var vector = (byte[])reader.GetValue(1);

                var floatVector = new float[vector.Length / 4];
                Buffer.BlockCopy(vector, 0, floatVector, 0, vector.Length);

                var score = CosineSimilarity(queryEmbedding, floatVector);
                results.Add((chunkId, score));
            }

            return results.OrderByDescending(r => r.score).Take(topK);
        }

        private float CosineSimilarity(float[] vec1, float[] vec2)
        {
            var dotProduct = vec1.Zip(vec2, (a, b) => a * b).Sum();
            var norm1 = Math.Sqrt(vec1.Sum(x => x * x));
            var norm2 = Math.Sqrt(vec2.Sum(x => x * x));

            if (norm1 == 0 || norm2 == 0)
                return 0;

            return (float)(dotProduct / (norm1 * norm2));
        }
    }
}
