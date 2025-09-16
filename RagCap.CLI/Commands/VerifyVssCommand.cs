using RagCap.Core.Search;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading.Tasks;

namespace RagCap.CLI.Commands
{
    public class VerifyVssCommand : AsyncCommand<VerifyVssCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--vss-path")]
            public string? VssPath { get; set; }

            [CommandOption("--vss-module")]
            public string? VssModule { get; set; }

            [CommandOption("--vss-search-func")]
            public string? VssSearchFunc { get; set; }

            [CommandOption("--vss-fromblob-func")]
            public string? VssFromBlobFunc { get; set; }

            [CommandOption("--dim")]
            public int Dimension { get; set; } = 384;
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var opts = new VssOptions
            {
                Path = settings.VssPath ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_PATH"),
                Module = settings.VssModule ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_MODULE") ?? "vss0",
                SearchFunction = settings.VssSearchFunc ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_SEARCH") ?? "vss_search",
                FromBlobFunction = settings.VssFromBlobFunc ?? System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_FROMBLOB")
            };

            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                conn.Open();

                // Load extension
                conn.EnableExtensions(true);
                if (string.IsNullOrWhiteSpace(opts.Path))
                {
                    AnsiConsole.MarkupLine("[red]RAGCAP_SQLITE_VSS_PATH not set and --vss-path not provided.[/]");
                    return Task.FromResult(1);
                }
                conn.LoadExtension(opts.Path);
                AnsiConsole.MarkupLine($"[green]Loaded extension from:[/] {opts.Path}");

                // Create table
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = $"CREATE VIRTUAL TABLE embeddings_vss USING {opts.Module}(embedding FLOAT[{settings.Dimension}]);";
                        cmd.ExecuteNonQuery();
                    }
                    catch
                    {
                        cmd.CommandText = $"CREATE VIRTUAL TABLE embeddings_vss USING {opts.Module}(embedding({settings.Dimension}));";
                        cmd.ExecuteNonQuery();
                    }
                }
                AnsiConsole.MarkupLine($"[green]Created VSS table with module:[/] {opts.Module}");

                // Insert dummy vector
                using (var ins = conn.CreateCommand())
                {
                    if (!string.IsNullOrWhiteSpace(opts.FromBlobFunction))
                    {
                        ins.CommandText = $"INSERT INTO embeddings_vss(rowid, embedding) VALUES (1, {opts.FromBlobFunction}(zeroblob({settings.Dimension * 4})));";
                    }
                    else
                    {
                        ins.CommandText = $"INSERT INTO embeddings_vss(rowid, embedding) VALUES (1, zeroblob({settings.Dimension * 4}));";
                    }
                    ins.ExecuteNonQuery();
                }
                AnsiConsole.MarkupLine("[green]Inserted 1 dummy vector.[/]");

                // Search
                using (var sel = conn.CreateCommand())
                {
                    sel.CommandText = $"SELECT rowid, distance FROM {opts.SearchFunction}(embeddings_vss, embedding, zeroblob({settings.Dimension * 4}), 1);";
                    using var r = sel.ExecuteReader();
                    if (r.Read())
                    {
                        AnsiConsole.MarkupLine("[green]Search function works:[/] returned 1 row");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]Search function returned no rows (still OK).[/]");
                    }
                }

                AnsiConsole.MarkupLine("[green]VSS verification complete.[/]");
                return Task.FromResult(0);
            }
            catch (System.Exception ex)
            {
                AnsiConsole.MarkupLine("[red]VSS verification failed:[/]");
                AnsiConsole.WriteException(ex);
                return Task.FromResult(1);
            }
        }
    }
}
