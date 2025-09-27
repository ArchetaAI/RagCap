using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Pipeline;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    // Attempts to use SQLite VSS extension for ANN search; falls back should be handled by caller.
    public class VssVectorSearcher : ISearcher
    {
        private readonly CapsuleManager capsuleManager;
        private readonly IEmbeddingProvider embeddingProvider;
        private readonly VssOptions options;

        public VssVectorSearcher(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider, VssOptions? options = null)
        {
            this.capsuleManager = capsuleManager;
            this.embeddingProvider = embeddingProvider;
            this.options = options ?? VssOptions.FromEnvironment();
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK, string? includePath = null, string? excludePath = null)
        {
            var q = await embeddingProvider.GenerateEmbeddingAsync(query);

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            TryLoadVss(connection);

            var dim = await GetEmbeddingDimension(connection);
            EnsureVssTable(connection, dim);
            SqlFilterUtil.EnsurePathIndex(connection);
            if (options.ForceReindex)
            {
                RebuildVssTable(connection);
            }
            else
            {
                PopulateVssTableIfNeeded(connection, dim);
            }

            // Pack query vector as blob of float32 in little-endian
            var qblob = new byte[q.Length * 4];
            Buffer.BlockCopy(q, 0, qblob, 0, qblob.Length);

            using var cmd = connection.CreateCommand();
            // Function/module names can differ across builds; allow override via env var
            var searchFunc = options.SearchFunction ?? "vss_search";
            cmd.CommandText = $@"
                SELECT c.id, s.path, c.text, v.distance
                FROM {searchFunc}(embeddings_vss, embedding, $qvec, $k) AS v
                JOIN chunks c ON c.id = v.rowid
                JOIN sources s ON s.id = c.source_id";
            cmd.Parameters.AddWithValue("$qvec", qblob);
            var filter = SqlFilterUtil.BuildPathFilterClause(cmd, includePath, excludePath, "s.path");
            if (!string.IsNullOrEmpty(filter))
            {
                cmd.CommandText += " WHERE " + filter;
            }
            cmd.CommandText += " ORDER BY v.distance ASC LIMIT $k;";
            cmd.Parameters.AddWithValue("$k", topK);

            var results = new List<SearchResult>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResult
                {
                    ChunkId = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Text = reader.GetString(2),
                    Score = (float)(1.0 / (1e-9 + reader.GetDouble(3)))
                });
            }
            return results;
        }

        private void TryLoadVss(SqliteConnection conn)
        {
            var path = options.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException("RAGCAP_SQLITE_VSS_PATH not set; provide full path to the SQLite VSS extension DLL/SO.");
            }
            try
            {
                conn.EnableExtensions(true);
                conn.LoadExtension(path);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("Failed to load SQLite VSS extension from path '" + path + "': " + ex.Message);
            }
        }

        private async Task<int> GetEmbeddingDimension(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) throw new Exception("No embeddings found.");
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private void EnsureVssTable(SqliteConnection conn, int dim)
        {
            var module = options.Module ?? "vss0";
            using var cmd = conn.CreateCommand();
            try
            {
                cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS embeddings_vss USING {module}(embedding FLOAT[{dim}]);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // fallback DDL shape used by some builds
                cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS embeddings_vss USING {module}(embedding({dim}));";
                cmd.ExecuteNonQuery();
            }
        }

        private void PopulateVssTableIfNeeded(SqliteConnection conn, int dim)
        {
            long embCount = 0, vssCount = -1;
            using (var c1 = conn.CreateCommand())
            {
                c1.CommandText = "SELECT COUNT(*) FROM embeddings;";
                embCount = (long)(c1.ExecuteScalar() ?? 0);
            }
            try
            {
                using var c2 = conn.CreateCommand();
                c2.CommandText = "SELECT COUNT(*) FROM embeddings_vss;";
                vssCount = (long)(c2.ExecuteScalar() ?? -1);
            }
            catch { vssCount = -1; }

            var fp = ComputeEmbeddingsFingerprint(conn);
            var currentIndexFp = GetMeta(conn, "vss_index_fp");
            if (vssCount == embCount && embCount > 0 && string.Equals(fp, currentIndexFp, StringComparison.Ordinal))
            {
                return;
            }

            using var tx = conn.BeginTransaction();
            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM embeddings_vss;";
                del.ExecuteNonQuery();
            }
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                var fromBlob = options.FromBlobFunction;
                if (!string.IsNullOrWhiteSpace(fromBlob))
                {
                    ins.CommandText = $"INSERT INTO embeddings_vss(rowid, embedding) SELECT chunk_id, {fromBlob}(vector) FROM embeddings;";
                    ins.ExecuteNonQuery();
                }
                else
                {
                    ins.CommandText = "INSERT INTO embeddings_vss(rowid, embedding) SELECT chunk_id, vector FROM embeddings;";
                    ins.ExecuteNonQuery();
                }
            }
            tx.Commit();

            // Record fingerprint for freshness tracking
            SetMeta(conn, "vss_index_fp", fp);
        }

        private void RebuildVssTable(SqliteConnection conn)
        {
            using var tx = conn.BeginTransaction();
            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM embeddings_vss;";
                del.ExecuteNonQuery();
            }
            using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                var fromBlob = options.FromBlobFunction;
                if (!string.IsNullOrWhiteSpace(fromBlob))
                {
                    ins.CommandText = $"INSERT INTO embeddings_vss(rowid, embedding) SELECT chunk_id, {fromBlob}(vector) FROM embeddings;";
                    ins.ExecuteNonQuery();
                }
                else
                {
                    ins.CommandText = "INSERT INTO embeddings_vss(rowid, embedding) SELECT chunk_id, vector FROM embeddings;";
                    ins.ExecuteNonQuery();
                }
            }
            tx.Commit();
            var fp = ComputeEmbeddingsFingerprint(conn);
            SetMeta(conn, "vss_index_fp", fp);
        }

        private static string ComputeEmbeddingsFingerprint(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(length(vector)),0), COALESCE(MAX(dimension),0) FROM embeddings;";
            using var r = cmd.ExecuteReader();
            long count = 0, sum = 0, maxdim = 0;
            if (r.Read())
            {
                count = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                sum = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                maxdim = r.IsDBNull(2) ? 0 : r.GetInt64(2);
            }
            return $"{count}:{sum}:{maxdim}";
        }

        private static string? GetMeta(SqliteConnection conn, string key)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM meta WHERE key = $k;";
                cmd.Parameters.AddWithValue("$k", key);
                return cmd.ExecuteScalar() as string;
            }
            catch { return null; }
        }

        private static void SetMeta(SqliteConnection conn, string key, string value)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO meta(key, value) VALUES ($k,$v);";
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$v", value);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}
