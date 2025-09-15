
using RagCap.Core.Capsule;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public class InspectPipeline
    {
        private readonly string _capsulePath;

        public InspectPipeline(string capsulePath)
        {
            _capsulePath = capsulePath;
        }

        public async Task<InspectionResult> RunAsync()
        {
            using (var capsuleManager = new CapsuleManager(_capsulePath))
            {
                var result = new InspectionResult
                {
                    CapsulePath = _capsulePath,
                    Provider = await capsuleManager.GetMetaValueAsync("embedding_provider") ?? string.Empty,
                    Model = await capsuleManager.GetMetaValueAsync("embedding_model") ?? string.Empty,
                    Dimension = (int)await GetDimension(capsuleManager),
                    Sources = (int)await CountRows(capsuleManager, "sources"),
                    Chunks = (int)await CountRows(capsuleManager, "chunks"),
                    AvgChunkLength = await GetAverageChunkLength(capsuleManager),
                    Embeddings = (int)await CountRows(capsuleManager, "embeddings")
                };

                return result;
            }
        }

        private async Task<long> GetDimension(CapsuleManager capsuleManager)
        {
            using var cmd = capsuleManager.Connection.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            return result == null ? 0 : (long)result;
        }

        private async Task<long> CountRows(CapsuleManager capsuleManager, string tableName)
        {
            using var cmd = capsuleManager.Connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            var result = await cmd.ExecuteScalarAsync();
            return result == null ? 0 : (long)result;
        }

        private async Task<double> GetAverageChunkLength(CapsuleManager capsuleManager)
        {
            using var cmd = capsuleManager.Connection.CreateCommand();
            cmd.CommandText = "SELECT AVG(LENGTH(text)) FROM chunks;";
            var result = await cmd.ExecuteScalarAsync();
            return result == null ? 0 : (double)result;
        }
    }

    public class InspectionResult
    {
        public string CapsulePath { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Dimension { get; set; }
        public int Sources { get; set; }
        public int Chunks { get; set; }
        public double AvgChunkLength { get; set; }
        public int Embeddings { get; set; }
    }
}
