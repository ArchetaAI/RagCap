using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Export;

public abstract class ExporterBase : IExporter
{
    public abstract Task ExportAsync(string capsuleFilePath, string outputFilePath);

    protected async Task<(List<Chunk> chunks, List<Embedding> embeddings)> ReadCapsuleDataAsync(string capsuleFilePath)
    {
        var chunks = new List<Chunk>();
        var embeddings = new List<Embedding>();

        var connectionString = $"Data Source={capsuleFilePath}";
        using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();

            var chunkCommand = connection.CreateCommand();
            chunkCommand.CommandText = "SELECT Id, SourceDocumentId, Content, TokenCount FROM chunks";
            using (var reader = await chunkCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    chunks.Add(new Chunk
                    {
                        Id = reader.GetInt64(0),
                        SourceDocumentId = reader.GetString(1),
                        Content = reader.GetString(2),
                        TokenCount = reader.GetInt32(3)
                    });
                }
            }

            var embeddingCommand = connection.CreateCommand();
            embeddingCommand.CommandText = "SELECT ChunkId, Vector, Dimension FROM embeddings";
            using (var reader = await embeddingCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var vectorBlob = new byte[reader.GetBytes(1, 0, null, 0, int.MaxValue)];
                    reader.GetBytes(1, 0, vectorBlob, 0, vectorBlob.Length);
                    var vector = new float[vectorBlob.Length / sizeof(float)];
                    Buffer.BlockCopy(vectorBlob, 0, vector, 0, vectorBlob.Length);

                    embeddings.Add(new Embedding
                    {
                        ChunkId = reader.GetString(0),
                        Vector = vector,
                        Dimension = reader.GetInt32(2)
                    });
                }
            }
        }

        return (chunks, embeddings);
    }
}