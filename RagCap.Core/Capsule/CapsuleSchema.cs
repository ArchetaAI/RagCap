using Microsoft.Data.Sqlite;
using System;

namespace RagCap.Core.Capsule
{
    public static class CapsuleSchema
    {
        // current schema version
        public const int CurrentVersion = 2;

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
                token_count INTEGER,
                FOREIGN KEY (source_id) REFERENCES sources(id)
            );

            CREATE TABLE IF NOT EXISTS embeddings (
                id INTEGER PRIMARY KEY,
                chunk_id INTEGER NOT NULL,
                vector BLOB NOT NULL,
                dimension INTEGER NOT NULL,
                FOREIGN KEY (chunk_id) REFERENCES chunks(id)
            );

            CREATE TABLE IF NOT EXISTS meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
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
            using var cmd = conn.CreateCommand();
            if (fromVersion < 2 && toVersion >= 2)
            {
                // Add token_count column if missing
                cmd.CommandText = "PRAGMA table_info(chunks);";
                using var reader = cmd.ExecuteReader();
                bool hasTokenCount = false;
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "token_count", StringComparison.OrdinalIgnoreCase))
                    {
                        hasTokenCount = true;
                        break;
                    }
                }
                reader.Close();

                if (!hasTokenCount)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE chunks ADD COLUMN token_count INTEGER;";
                    alter.ExecuteNonQuery();

                    // Backfill token_count by a simple whitespace tokenization
                    // (keeps migration lightweight; future builds will write accurate counts)
                    using var sel = conn.CreateCommand();
                    sel.CommandText = "SELECT id, text FROM chunks;";
                    using var rdr = sel.ExecuteReader();
                    var updates = new List<(long id, int tokens)>();
                    while (rdr.Read())
                    {
                        var id = rdr.GetInt64(0);
                        var text = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        int tokens = string.IsNullOrWhiteSpace(text) ? 0 : System.Text.RegularExpressions.Regex.Matches(text, "\\S+\\s*").Count;
                        updates.Add((id, tokens));
                    }
                    rdr.Close();

                    using var tx = conn.BeginTransaction();
                    foreach (var (id, tokens) in updates)
                    {
                        using var upd = conn.CreateCommand();
                        upd.Transaction = tx;
                        upd.CommandText = "UPDATE chunks SET token_count = $tc WHERE id = $id;";
                        upd.Parameters.AddWithValue("$tc", tokens);
                        upd.Parameters.AddWithValue("$id", id);
                        upd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }

                // Bump manifest version
                using var bump = conn.CreateCommand();
                bump.CommandText = "UPDATE manifest SET version = $v WHERE id = 1;";
                bump.Parameters.AddWithValue("$v", toVersion);
                bump.ExecuteNonQuery();
            }
        }
    }
}
