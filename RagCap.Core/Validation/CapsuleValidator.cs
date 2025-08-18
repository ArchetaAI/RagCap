using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace RagCap.Core.Validation
{
    public class CapsuleValidator
    {
        public ValidationResult Validate(string capsulePath)
        {
            if (!File.Exists(capsulePath))
                return ValidationResult.Fail("Capsule file does not exist.");

            try
            {
                using var conn = new SqliteConnection($"Data Source={capsulePath}");
                conn.Open();

                // manifest presence
                if (!TableHasRows(conn, "manifest"))
                    return ValidationResult.Fail("Manifest table is missing or empty.");

                // embedding dimension consistency
                if (!CheckEmbeddingDimensions(conn))
                    return ValidationResult.Fail("Inconsistent embedding dimensions detected.");

                // source file hash presence
                if (!ColumnHasNonNullValues(conn, "sources", "hash"))
                    return ValidationResult.Fail("Some source documents are missing hashes.");

                return ValidationResult.Ok("Capsule is valid.");
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail($"Validation failed: {ex.Message}");
            }
        }

        private bool TableHasRows(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }

        private bool ColumnHasNonNullValues(SqliteConnection conn, string table, string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column} IS NULL OR {column} = '';";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count == 0;
        }

        private bool CheckEmbeddingDimensions(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT dimension FROM embeddings;";
            using var reader = cmd.ExecuteReader();

            int? dim = null;
            while (reader.Read())
            {
                int current = reader.GetInt32(0);
                if (dim == null) dim = current;
                else if (dim != current) return false;
            }
            return true;
        }
    }

    public record ValidationResult(bool Success, string Message)
    {
        public static ValidationResult Ok(string msg) => new ValidationResult(true, msg);
        public static ValidationResult Fail(string msg) => new ValidationResult(false, msg);
    }
}
