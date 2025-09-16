using RagCap.Core.Search;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class VerifyVecCommand : AsyncCommand<VerifyVecCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--vec-path")]
            public string? VecPath { get; set; }

            [CommandOption("--vec-module")]
            public string? VecModule { get; set; } = "vec0";

            [CommandOption("--dim")]
            public int Dimension { get; set; } = 384;
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                conn.Open();

                var path = settings.VecPath ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_PATH");
                if (string.IsNullOrWhiteSpace(path))
                {
                    AnsiConsole.MarkupLine("[red]RAGCAP_SQLITE_VEC_PATH not set and --vec-path not provided.[/]");
                    return Task.FromResult(1);
                }
                conn.EnableExtensions(true);
                conn.LoadExtension(path);
                AnsiConsole.MarkupLine($"[green]Loaded sqlite-vec from:[/] {path}");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"CREATE VIRTUAL TABLE t USING {settings.VecModule}(embedding FLOAT[{settings.Dimension}]);";
                    cmd.ExecuteNonQuery();
                }
                AnsiConsole.MarkupLine($"[green]Created vec table with module:[/] {settings.VecModule}");

                // Build a zero vector JSON array of the given dimension
                var zeros = new string[settings.Dimension];
                for (int i = 0; i < zeros.Length; i++) zeros[i] = "0";
                var zeroList = string.Join(",", zeros);

                using (var ins = conn.CreateCommand())
                {
                    ins.CommandText = $"INSERT INTO t(rowid, embedding) VALUES (1, json_array({zeroList}));";
                    ins.ExecuteNonQuery();
                }

                using (var sel = conn.CreateCommand())
                {
                    sel.CommandText = $"SELECT rowid, distance FROM t WHERE embedding MATCH json_array({zeroList}) ORDER BY distance LIMIT 1;";
                    using var r = sel.ExecuteReader();
                    if (r.Read())
                    {
                        AnsiConsole.MarkupLine("[green]sqlite-vec MATCH search works.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]Search returned no rows (still OK).[/]");
                    }
                }

                return Task.FromResult(0);
            }
            catch (System.Exception ex)
            {
                AnsiConsole.MarkupLine("[red]sqlite-vec verification failed:[/]");
                AnsiConsole.WriteException(ex);
                return Task.FromResult(1);
            }
        }
    }
}
