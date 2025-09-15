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

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK)
        {
            var results = new List<SearchResult>();

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.id, s.path, c.text, bm25(chunks_fts) as score
                FROM chunks_fts
                JOIN chunks c ON c.id = chunks_fts.rowid
                JOIN sources s ON s.id = c.source_id
                WHERE chunks_fts MATCH $query 
                ORDER BY score 
                LIMIT $limit";
            command.Parameters.AddWithValue("$query", EscapeFtsQuery(query));
            command.Parameters.AddWithValue("$limit", topK);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResult
                {
                    ChunkId = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Text = reader.GetString(2),
                    Score = reader.GetFloat(3)
                });
            }

            return results;
        }

        private string EscapeFtsQuery(string query)
        {
            // Escape double quotes by doubling them.
            string escapedQuery = query.Replace("\"", "\"\"");

            // Escape other special FTS5 characters.
            escapedQuery = escapedQuery.Replace("-", "\"-\"");
            escapedQuery = escapedQuery.Replace("*", "\"*\"");
            escapedQuery = escapedQuery.Replace("^", "\"^\"");
            escapedQuery = escapedQuery.Replace("$", "\"$\"");
            escapedQuery = escapedQuery.Replace("(", "\"(\"");
            escapedQuery = escapedQuery.Replace(")", "\")\")");

            // Enclose the entire query in double quotes.
            return "\"" + escapedQuery + "\"";
        }
    }
}