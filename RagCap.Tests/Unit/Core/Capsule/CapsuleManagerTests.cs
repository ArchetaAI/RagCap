using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using System.Threading.Tasks;
using Xunit;

namespace RagCap.Tests.Unit.Core.Capsule
{
    public class CapsuleManagerTests
    {
        [Fact]
        public async Task CreateTablesAsync_ShouldCreateAllTables()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                using (var manager = new CapsuleManager(connection))
                {
                    await manager.CreateTablesAsync();

                    var tables = new[] { "meta", "source_documents", "chunks", "embeddings", "fts_idx" };
                    foreach (var table in tables)
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}';";
                        var result = await command.ExecuteScalarAsync();
                        Assert.Equal(table, result);
                    }
                }
            }
        }
    }
}
