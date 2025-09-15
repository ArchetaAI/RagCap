
using RagCap.Core.Capsule;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using RagCap.Core.Utils;

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
            // Compute true token average using the same tokenizer as the chunker
            var tokenizer = new Tokenizer();
            double totalTokens = 0;
            long count = 0;

            using var cmd = capsuleManager.Connection.CreateCommand();
            cmd.CommandText = "SELECT text FROM chunks;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var text = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                totalTokens += tokenizer.CountTokens(text);
                count++;
            }

            if (count == 0) return 0;
            return totalTokens / count;
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
