using Microsoft.Data.Sqlite;
using System.IO;

namespace RagCap.Tests.Integration;

public static class TestCapsule
{
    public static void Create(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();

            var createChunksTable = connection.CreateCommand();
            createChunksTable.CommandText = "CREATE TABLE chunks (Id INTEGER PRIMARY KEY, SourceDocumentId TEXT, Content TEXT, TokenCount INTEGER)";
            createChunksTable.ExecuteNonQuery();

            var createEmbeddingsTable = connection.CreateCommand();
            createEmbeddingsTable.CommandText = "CREATE TABLE embeddings (ChunkId TEXT, Vector BLOB, Dimension INTEGER)";
            createEmbeddingsTable.ExecuteNonQuery();

            var insertChunk = connection.CreateCommand();
            insertChunk.CommandText = "INSERT INTO chunks (Id, SourceDocumentId, Content, TokenCount) VALUES (1, 'test.txt', 'This is a test chunk.', 5)";
            insertChunk.ExecuteNonQuery();

            var insertEmbedding = connection.CreateCommand();
            insertEmbedding.CommandText = "INSERT INTO embeddings (ChunkId, Vector, Dimension) VALUES ('1', @vector, 4)";
            var vector = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            var vectorBlob = new byte[vector.Length * sizeof(float)];
            System.Buffer.BlockCopy(vector, 0, vectorBlob, 0, vectorBlob.Length);
            insertEmbedding.Parameters.AddWithValue("@vector", vectorBlob);
            insertEmbedding.ExecuteNonQuery();
        }
    }
}