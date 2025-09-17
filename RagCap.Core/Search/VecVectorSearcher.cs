using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RagCap.Core.Pipeline;

namespace RagCap.Core.Search
{
    // Uses sqlite-vec (vec0) module with MATCH operator
    public class VecVectorSearcher : ISearcher
    {
        private readonly CapsuleManager capsuleManager;
        private readonly IEmbeddingProvider embeddingProvider;
        private readonly VecOptions options;

        public VecVectorSearcher(CapsuleManager capsuleManager, IEmbeddingProvider embeddingProvider, VecOptions? options = null)
        {
            this.capsuleManager = capsuleManager;
            this.embeddingProvider = embeddingProvider;
            this.options = options ?? VecOptions.FromEnvironment();
        }

        public async Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK)
        {
            var q = await embeddingProvider.GenerateEmbeddingAsync(query);

            using var connection = capsuleManager.Connection;
            await connection.OpenAsync();

            LoadVecExtension(connection);

            var dim = await GetEmbeddingDimension(connection);
            EnsureVecTable(connection, dim);
            await PopulateVecTableIfNeeded(connection, dim);

            var qjson = SerializeVectorToJson(q);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                SELECT c.id, s.path, c.text, embeddings_vec.distance
                FROM embeddings_vec
                JOIN chunks c ON c.id = embeddings_vec.rowid
                JOIN sources s ON s.id = c.source_id
                WHERE embedding MATCH $q
                ORDER BY embeddings_vec.distance ASC
                LIMIT $k;";
            cmd.Parameters.AddWithValue("$q", qjson);
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

        private void LoadVecExtension(SqliteConnection conn)
        {
            var path = options.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                // Fallback to environment
                var env = VecOptions.FromEnvironment();
                path = env.Path;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                // Try simple auto-discovery in common locations
                var baseDir = AppContext.BaseDirectory;
                var cwd = Environment.CurrentDirectory;
                var candidates = new List<string>();

                foreach (var name in PossibleLibraryNames())
                {
                    candidates.Add(Path.Combine(baseDir, name));
                    candidates.Add(Path.Combine(cwd, name));
                    candidates.Add(Path.Combine(baseDir, "native", RuntimeIdFolder(), name));
                    candidates.Add(Path.Combine(cwd, "native", RuntimeIdFolder(), name));
                }

                path = candidates.FirstOrDefault(File.Exists) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new NotSupportedException("sqlite-vec path not found. Set --vec-path or RAGCAP_SQLITE_VEC_PATH to the sqlite-vec library.");
            }

            conn.EnableExtensions(true);
            try
            {
                conn.LoadExtension(path);
            }
            catch
            {
                // Fallback to common entry point name for sqlite-vec
                conn.LoadExtension(path, "sqlite3_vec_init");
            }
        }

        private static IEnumerable<string> PossibleLibraryNames()
        {
            if (OperatingSystem.IsWindows())
            {
                yield return "sqlite-vec.dll";
                yield return "vec0.dll";
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "sqlite-vec.dylib";
                yield return "vec0.dylib";
            }
            else // Linux and others
            {
                yield return "sqlite-vec.so";
                yield return "vec0.so";
            }
        }

        private static string RuntimeIdFolder()
        {
            if (OperatingSystem.IsWindows()) return Environment.Is64BitProcess ? "win-x64" : "win-x86";
            if (OperatingSystem.IsMacOS()) return Environment.Is64BitProcess ? "osx-arm64" : "osx-x64"; // heuristic
            if (OperatingSystem.IsLinux())
            {
                // Distinguish arch only; libc (glibc/musl) cannot be reliably detected here
                return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            }
            return "unknown";
        }

        private async Task<int> GetEmbeddingDimension(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) throw new Exception("No embeddings found.");
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private void EnsureVecTable(SqliteConnection conn, int dim)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS embeddings_vec USING {options.Module}(embedding FLOAT[{dim}]);";
            cmd.ExecuteNonQuery();
        }

        private async Task PopulateVecTableIfNeeded(SqliteConnection conn, int dim)
        {
            // If counts match, assume populated
            long embCount = 0, vecCount = -1;
            using (var c1 = conn.CreateCommand())
            {
                c1.CommandText = "SELECT COUNT(*) FROM embeddings;";
                embCount = (long)(await c1.ExecuteScalarAsync() ?? 0);
            }
            try
            {
                using var c2 = conn.CreateCommand();
                c2.CommandText = "SELECT COUNT(*) FROM embeddings_vec;";
                vecCount = (long)(await c2.ExecuteScalarAsync() ?? -1);
            }
            catch { vecCount = -1; }

            if (vecCount == embCount && embCount > 0)
            {
                return;
            }

            using var tx = conn.BeginTransaction();
            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM embeddings_vec;";
                del.ExecuteNonQuery();
            }

            using (var sel = conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT chunk_id, vector FROM embeddings;";
                using var r = sel.ExecuteReader();
                while (r.Read())
                {
                    var rowid = r.GetInt64(0);
                    var blob = (byte[])r.GetValue(1);
                    var vec = new float[blob.Length / 4];
                    Buffer.BlockCopy(blob, 0, vec, 0, blob.Length);
                    var json = SerializeVectorToJson(vec);

                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO embeddings_vec(rowid, embedding) VALUES ($id, $json);";
                    ins.Parameters.AddWithValue("$id", rowid);
                    ins.Parameters.AddWithValue("$json", json);
                    ins.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }

        private static string SerializeVectorToJson(float[] v)
        {
            // Use InvariantCulture to ensure dot as decimal separator
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(v[i].ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
