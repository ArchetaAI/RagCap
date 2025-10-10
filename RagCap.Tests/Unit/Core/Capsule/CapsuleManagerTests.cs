using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using System.Threading.Tasks;
using Xunit;

namespace RagCap.Tests.Unit.Core.Capsule
{
    public class CapsuleManagerTests
    {
        [Fact]
        public async Task InitializeSchema_ShouldCreateAllTables()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                // Initialize schema directly (constructor-based init is for file-backed capsules)
                CapsuleSchema.InitializeSchema(connection);

                var tables = new[] { "manifest", "sources", "chunks", "embeddings", "meta", "chunks_fts" };
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
