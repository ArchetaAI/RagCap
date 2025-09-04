
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public class BM25Searcher
    {
        private readonly SqliteConnection _connection;

        public BM25Searcher(SqliteConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<(long chunk_id, float score)>> SearchAsync(string query, int topK)
        {
            var results = new List<(long chunk_id, float score)>();

            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT rowid, bm25(chunks_fts) FROM chunks_fts WHERE chunks_fts MATCH $query ORDER BY bm25(chunks_fts) LIMIT $limit";
            command.Parameters.AddWithValue("$query", query);
            command.Parameters.AddWithValue("$limit", topK);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add((reader.GetInt64(0), (float)reader.GetDouble(1)));
            }

            return results;
        }
    }
}
