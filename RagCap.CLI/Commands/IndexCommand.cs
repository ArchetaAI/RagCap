using RagCap.Core.Capsule;
using RagCap.Core.Search;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace RagCap.CLI.Commands
{
    public class IndexCommand : AsyncCommand<IndexCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<capsule>")]
            public required string Capsule { get; set; }

            [CommandOption("--vec-path")]
            public string? VecPath { get; set; }

            [CommandOption("--vec-module")]
            [DefaultValue("vec0")]
            public string VecModule { get; set; } = "vec0";
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            if (!System.IO.File.Exists(settings.Capsule))
            {
                AnsiConsole.MarkupLine($"[red]Error: Capsule not found:[/] {settings.Capsule}");
                return 1;
            }

            try
            {
                using var cap = new CapsuleManager(settings.Capsule);
                var opts = new VecOptions
                {
                    Path = settings.VecPath ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_PATH"),
                    Module = settings.VecModule
                };

                using var conn = cap.Connection;
                await conn.OpenAsync();

                // Load extension on this connection and reuse it for all operations
                LoadVecExtension(conn, opts);

                var dim = await GetDim(conn);
                EnsureVec(conn, dim, opts);
                await PopulateVec(conn, opts);
                AnsiConsole.MarkupLine("[green]sqlite-vec index built successfully.[/]");
                return 0;
            }
            catch (System.Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Index build failed:[/]");
                AnsiConsole.WriteException(ex);
                return 1;
            }
        }

        private async Task<int> GetDim(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == System.DBNull.Value ? 0 : System.Convert.ToInt32(result);
        }

        private void LoadVecExtension(SqliteConnection conn, VecOptions opts)
        {
            conn.EnableExtensions(true);
            if (string.IsNullOrWhiteSpace(opts.Path))
            {
                throw new System.Exception("RAGCAP_SQLITE_VEC_PATH not set and --vec-path not provided.");
            }
            // Attempt to load the extension with common entry points
            try
            {
                conn.LoadExtension(opts.Path);
            }
            catch
            {
                try
                {
                    // Many sqlite-vec builds export sqlite3_vec_init
                    conn.LoadExtension(opts.Path, "sqlite3_vec_init");
                }
                catch (System.Exception ex2)
                {
                    throw new System.Exception($"Failed to load sqlite-vec extension from path '{opts.Path}'. Tried default and 'sqlite3_vec_init' entry points.", ex2);
                }
            }
        }

        private void EnsureVec(SqliteConnection conn, int dim, VecOptions opts)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS embeddings_vec USING {opts.Module}(embedding FLOAT[{dim}]);";
            cmd.ExecuteNonQuery();
        }

        private async Task PopulateVec(SqliteConnection conn, VecOptions opts)
        {
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
                using var r = await sel.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var rowid = r.GetInt64(0);
                    var blob = (byte[])r.GetValue(1);
                    var vec = new float[blob.Length / 4];
                    System.Buffer.BlockCopy(blob, 0, vec, 0, blob.Length);
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
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(v[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
