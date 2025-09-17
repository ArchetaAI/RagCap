using Microsoft.Data.Sqlite;
using RagCap.Core.Capsule;
using System.Threading.Tasks;

namespace RagCap.Core.Capsule
{
    /// <summary>
    /// Manages the lifecycle of a RagCap capsule.
    /// </summary>
    public class CapsuleManager : System.IDisposable
    {
        private readonly SqliteConnection _connection;
        /// <summary>
        /// Gets a new connection to the capsule database.
        /// </summary>
        public SqliteConnection Connection => new SqliteConnection(_connection.ConnectionString);

        /// <summary>
        /// Initializes a new instance of the <see cref="CapsuleManager"/> class.
        /// </summary>
        /// <param name="capsulePath">The path to the capsule file.</param>
        public CapsuleManager(string capsulePath) : this(new SqliteConnection($"Data Source={capsulePath}"))
        {
            bool newCapsule = !File.Exists(capsulePath);
            _connection.Open();
            if (newCapsule)
            {
                CapsuleSchema.InitializeSchema(_connection);
            }
            else
            {
                // Attempt schema upgrade if needed
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT version FROM manifest WHERE id = 1;";
                var result = cmd.ExecuteScalar();
                var current = result == null ? 1 : Convert.ToInt32(result);
                if (current < CapsuleSchema.CurrentVersion)
                {
                    CapsuleSchema.UpgradeSchema(_connection, current, CapsuleSchema.CurrentVersion);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CapsuleManager"/> class.
        /// </summary>
        /// <param name="connection">The SQLite connection to use.</param>
        public CapsuleManager(SqliteConnection connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Adds a source document to the capsule.
        /// </summary>
        /// <param name="document">The source document to add.</param>
        /// <returns>The ID of the newly added source document.</returns>
        public async Task<long> AddSourceDocumentAsync(SourceDocument document)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO sources (path, hash) VALUES ($path, $hash) RETURNING id;";
            cmd.Parameters.AddWithValue("$path", document.Path);
            cmd.Parameters.AddWithValue("$hash", document.Hash);
            return (long?)await cmd.ExecuteScalarAsync() ?? 0;
        }

        /// <summary>
        /// Adds a chunk to the capsule.
        /// </summary>
        /// <param name="chunk">The chunk to add.</param>
        /// <returns>The ID of the newly added chunk.</returns>
        public async Task<long> AddChunkAsync(Chunk chunk)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO chunks (source_id, text, token_count) VALUES ($source_id, $text, $token_count) RETURNING id;";
            cmd.Parameters.AddWithValue("$source_id", chunk.SourceDocumentId);
            cmd.Parameters.AddWithValue("$text", chunk.Content);
            cmd.Parameters.AddWithValue("$token_count", chunk.TokenCount);
            return (long?)await cmd.ExecuteScalarAsync() ?? 0;
        }

        /// <summary>
        /// Adds an embedding to the capsule.
        /// </summary>
        /// <param name="embedding">The embedding to add.</param>
        public async Task AddEmbeddingAsync(Embedding embedding)
        {
#nullable disable
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO embeddings (chunk_id, vector, dimension) VALUES ($chunk_id, $vector, $dimension);";

            var vectorBytes = new byte[embedding.Vector!.Length * 4];
            Buffer.BlockCopy(embedding.Vector, 0, vectorBytes, 0, vectorBytes.Length);

            cmd.Parameters.AddWithValue("$chunk_id", embedding.ChunkId);
            cmd.Parameters.AddWithValue("$vector", vectorBytes);
            cmd.Parameters.AddWithValue("$dimension", embedding.Dimension);
            await cmd.ExecuteNonQueryAsync();
#nullable enable
        }

        /// <summary>
        /// Sets a metadata value in the capsule.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        public async Task SetMetaValueAsync(string key, string value)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO meta (key, value) VALUES ($key, $value);";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Gets a metadata value from the capsule.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value.</returns>
        public async Task<string?> GetMetaValueAsync(string key)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return (string?)await cmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// Gets a chunk from the capsule.
        /// </summary>
        /// <param name="chunkId">The ID of the chunk to get.</param>
        /// <returns>The chunk.</returns>
        public async Task<Chunk?> GetChunkAsync(long chunkId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id, source_id, text FROM chunks WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", chunkId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Chunk
                {
                    Id = reader.GetInt64(0),
                    SourceDocumentId = reader.GetInt64(1).ToString(),
                    Content = reader.GetString(2)
                };
            }
            return null;
        }

        /// <summary>
        /// Gets a source document from the capsule.
        /// </summary>
        /// <param name="sourceId">The ID of the source document to get.</param>
        /// <returns>The source document.</returns>
        public async Task<SourceDocument?> GetSourceDocumentAsync(long sourceId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id, path, hash FROM sources WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", sourceId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SourceDocument
                {
                    Id = reader.GetInt64(0).ToString(),
                    Path = reader.GetString(1),
                    Hash = reader.GetString(2)
                };
            }
            return null;
        }

        /// <summary>
        /// Gets an embedding from the capsule.
        /// </summary>
        /// <param name="chunkId">The ID of the chunk to get the embedding for.</param>
        /// <returns>The embedding.</returns>
        public async Task<Embedding?> GetEmbeddingAsync(string chunkId)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT chunk_id, vector, dimension FROM embeddings WHERE chunk_id = $chunk_id;";
            cmd.Parameters.AddWithValue("$chunk_id", chunkId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var value = reader.GetValue(1);
                if (value is DBNull || value == null) return null;
                var vectorBytes = (byte[])value;
                var vector = new float[vectorBytes.Length / 4];
                Buffer.BlockCopy(vectorBytes, 0, vector, 0, vectorBytes.Length);

                return new Embedding
                {
                    ChunkId = reader.GetString(0),
                    Vector = vector,
                    Dimension = reader.GetInt32(2)
                };
            }
            return null;
        }

        /// <summary>
        /// Gets all chunks from the capsule.
        /// </summary>
        /// <returns>A list of all chunks.</returns>
        public async Task<List<Chunk>> GetAllChunksAsync()
        {
            var chunks = new List<Chunk>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id, source_id, text FROM chunks;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chunks.Add(new Chunk
                {
                    Id = reader.GetInt64(0),
                    SourceDocumentId = reader.GetInt64(1).ToString(),
                    Content = reader.GetString(2)
                });
            }
            return chunks;
        }

        /// <summary>
        /// Gets all source documents from the capsule.
        /// </summary>
        /// <returns>A list of all source documents.</returns>
        public async Task<List<SourceDocument>> GetAllSourceDocumentsAsync()
        {
            var documents = new List<SourceDocument>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT id, path, hash FROM sources;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                documents.Add(new SourceDocument
                {
                    Id = reader.GetInt64(0).ToString(),
                    Path = reader.GetString(1),
                    Hash = reader.GetString(2)
                });
            }
            return documents;
        }

        /// <summary>
        /// Gets all metadata from the capsule.
        /// </summary>
        /// <returns>A dictionary of all metadata.</returns>
        public async Task<Dictionary<string, string>> GetAllMetaAsync()
        {
            var meta = new Dictionary<string, string>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM meta;";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                meta[reader.GetString(0)] = reader.GetString(1);
            }
            return meta;
        }

        /// <summary>
        /// Disposes the capsule manager.
        /// </summary>
        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}