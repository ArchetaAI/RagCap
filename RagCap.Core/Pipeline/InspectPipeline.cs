
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
                // Fallback: if local provider and model missing, show default local model name
                if (string.Equals(result.Provider, "local", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(result.Model))
                {
                    result.Model = "all-MiniLM-L6-v2";
                }
                return result;
            }
        }

        private async Task<long> GetDimension(CapsuleManager capsuleManager)
        {
            using var conn = capsuleManager.Connection;
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT dimension FROM embeddings LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private async Task<long> CountRows(CapsuleManager capsuleManager, string tableName)
        {
            using var conn = capsuleManager.Connection;
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }

        private async Task<double> GetAverageChunkLength(CapsuleManager capsuleManager)
        {
            using var conn = capsuleManager.Connection;
            await conn.OpenAsync();

            // Prefer persisted token_count if available for performance
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT AVG(token_count) FROM chunks WHERE token_count IS NOT NULL;";
            var avg = await cmd.ExecuteScalarAsync();
            if (avg != null && avg != DBNull.Value)
            {
                return Convert.ToDouble(avg);
            }

            // Fallback: compute using shared tokenizer
            var tokenizer = new Tokenizer();
            double totalTokens = 0;
            long count = 0;
            cmd.CommandText = "SELECT text FROM chunks;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var text = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                totalTokens += tokenizer.CountTokens(text);
                count++;
            }
            return count == 0 ? 0 : totalTokens / count;
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
