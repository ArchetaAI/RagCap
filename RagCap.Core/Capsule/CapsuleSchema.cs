using Microsoft.Data.Sqlite;
using System;

namespace RagCap.Core.Capsule
{
    public static class CapsuleSchema
    {
        // current schema version
        public const int CurrentVersion = 1;

        public static void InitializeSchema(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS manifest (
                id INTEGER PRIMARY KEY,
                version INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sources (
                id INTEGER PRIMARY KEY,
                path TEXT NOT NULL,
                hash TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS chunks (
                id INTEGER PRIMARY KEY,
                source_id INTEGER NOT NULL,
                text TEXT NOT NULL,
                FOREIGN KEY (source_id) REFERENCES sources(id)
            );

            CREATE TABLE IF NOT EXISTS embeddings (
                id INTEGER PRIMARY KEY,
                chunk_id INTEGER NOT NULL,
                vector BLOB NOT NULL,
                dimension INTEGER NOT NULL,
                FOREIGN KEY (chunk_id) REFERENCES chunks(id)
            );

                        CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(text, content='chunks', content_rowid='id');

            CREATE TRIGGER IF NOT EXISTS chunks_after_insert AFTER INSERT ON chunks BEGIN
              INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
            END;

            CREATE TRIGGER IF NOT EXISTS chunks_after_delete AFTER DELETE ON chunks BEGIN
              INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES('delete', old.id, old.text);
            END;

            CREATE TRIGGER IF NOT EXISTS chunks_after_update AFTER UPDATE ON chunks BEGIN
              INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES('delete', old.id, old.text);
              INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
            END;
            ";
            cmd.ExecuteNonQuery();

            // insert manifest if not exists
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT OR IGNORE INTO manifest (id, version, created_at)
                VALUES (1, $version, $created_at);
            ";
            insertCmd.Parameters.AddWithValue("$version", CurrentVersion);
            insertCmd.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
            insertCmd.ExecuteNonQuery();
        }

        public static void UpgradeSchema(SqliteConnection conn, int fromVersion, int toVersion)
        {
            // migration logic will go here in the future
        }
    }
}
