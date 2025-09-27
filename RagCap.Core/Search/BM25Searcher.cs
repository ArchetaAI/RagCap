using RagCap.Core.Capsule;
using RagCap.Core.Pipeline;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public class BM25Searcher : ISearcher
    {
        private readonly CapsuleManager capsuleManager;

        public BM25Searcher(CapsuleManager capsuleManager)
        {
            this.capsuleManager = capsuleManager;
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK, string? includePath = null, string? excludePath = null)
        {
            var results = new List<SearchResult>();

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            SqlFilterUtil.EnsurePathIndex(connection);
            command.CommandText = @"
                SELECT c.id, s.path, c.text, bm25(chunks_fts) as score
                FROM chunks_fts
                JOIN chunks c ON c.id = chunks_fts.rowid
                JOIN sources s ON s.id = c.source_id
                WHERE chunks_fts MATCH $query";
            command.Parameters.AddWithValue("$query", BuildSafeFtsQuery(query));
            var filter = SqlFilterUtil.BuildPathFilterClause(command, includePath, excludePath, "s.path");
            if (!string.IsNullOrEmpty(filter))
            {
                command.CommandText += " AND " + filter + " ";
            }
            command.CommandText += " ORDER BY score LIMIT $limit";
            command.Parameters.AddWithValue("$limit", topK);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResult
                {
                    ChunkId = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Text = reader.GetString(2),
                    // bm25() returns lower-is-better; present higher-is-better
                    Score = (float)(1.0 / (1e-9 + reader.GetDouble(3)))
                });
            }

            return results;
        }

        public async Task<List<long>> SearchChunkIdsAsync(string query, int limit, string? includePath = null, string? excludePath = null)
        {
            var ids = new List<long>();

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            SqlFilterUtil.EnsurePathIndex(connection);
            command.CommandText = @"
                SELECT c.id
                FROM chunks_fts
                JOIN chunks c ON c.id = chunks_fts.rowid
                JOIN sources s ON s.id = c.source_id
                WHERE chunks_fts MATCH $query";
            command.Parameters.AddWithValue("$query", BuildSafeFtsQuery(query));
            var filter = SqlFilterUtil.BuildPathFilterClause(command, includePath, excludePath, "s.path");
            if (!string.IsNullOrEmpty(filter))
            {
                command.CommandText += " AND " + filter + " ";
            }
            command.CommandText += " ORDER BY bm25(chunks_fts) LIMIT $limit";
            command.Parameters.AddWithValue("$limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids;
        }

        private string BuildSafeFtsQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "\"\""; // match nothing
            }
            var matches = System.Text.RegularExpressions.Regex.Matches(query, @"[\p{L}\p{N}][\p{L}\p{N}_\-']*");
            var terms = new List<string>(matches.Count);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var t = m.Value.Trim();
                if (t.Length > 0) terms.Add(t);
            }
            const int MaxTerms = 12;
            if (terms.Count > MaxTerms)
            {
                terms = terms.GetRange(0, MaxTerms);
            }
            if (terms.Count == 0)
            {
                var escaped = query.Replace("\"", "\"\"");
                return "\"" + escaped + "\"";
            }
            for (int i = 0; i < terms.Count; i++)
            {
                terms[i] = "\"" + terms[i].Replace("\"", "\"\"") + "\"";
            }
            return string.Join(" OR ", terms);
        }
    }
}
