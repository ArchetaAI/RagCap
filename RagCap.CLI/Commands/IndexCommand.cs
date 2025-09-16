using RagCap.Core.Capsule;
using RagCap.Core.Search;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;

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
                var dim = await GetDim(cap);
                await EnsureVec(cap, dim, opts);
                await PopulateVec(cap, opts);
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

        private async Task<int> GetDim(CapsuleManager cap)
        {
            using var cmd = cap.Connection.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            return result == null ? 0 : System.Convert.ToInt32(result);
        }

        private Task EnsureVec(CapsuleManager cap, int dim, VecOptions opts)
        {
            var conn = cap.Connection;
            conn.EnableExtensions(true);
            if (string.IsNullOrWhiteSpace(opts.Path))
            {
                throw new System.Exception("RAGCAP_SQLITE_VEC_PATH not set and --vec-path not provided.");
            }
            conn.LoadExtension(opts.Path);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS embeddings_vec USING {opts.Module}(embedding FLOAT[{dim}]);";
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }

        private async Task PopulateVec(CapsuleManager cap, VecOptions opts)
        {
            using var tx = cap.Connection.BeginTransaction();
            using (var del = cap.Connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM embeddings_vec;";
                del.ExecuteNonQuery();
            }
            using (var sel = cap.Connection.CreateCommand())
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
                    using var ins = cap.Connection.CreateCommand();
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
