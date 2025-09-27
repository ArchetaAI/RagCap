using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace RagCap.Core.Search
{
    internal static class SqlFilterUtil
    {
        public static void EnsurePathIndex(SqliteConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_sources_path ON sources(path);";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static string BuildPathFilterClause(SqliteCommand cmd, string? include, string? exclude, string pathExpr = "s.path")
        {
            var inc = SplitPatterns(include);
            var exc = SplitPatterns(exclude);

            var incClause = BuildLikeClause(cmd, inc, pathExpr, "inc");
            var excClause = BuildLikeClause(cmd, exc, pathExpr, "exc");

            if (string.IsNullOrEmpty(incClause) && string.IsNullOrEmpty(excClause)) return string.Empty;

            if (!string.IsNullOrEmpty(incClause) && !string.IsNullOrEmpty(excClause))
            {
                return $"((" + incClause + ") AND NOT (" + excClause + "))";
            }
            if (!string.IsNullOrEmpty(incClause)) return "(" + incClause + ")";
            return "NOT (" + excClause + ")";
        }

        private static string BuildLikeClause(SqliteCommand cmd, List<string> patterns, string pathExpr, string prefix)
        {
            if (patterns.Count == 0) return string.Empty;

            var conds = new List<string>(patterns.Count);
            for (int i = 0; i < patterns.Count; i++)
            {
                var p = NormalizeGlobToLike(patterns[i]);
                var name = "$" + prefix + i;
                cmd.Parameters.AddWithValue(name, p);
                conds.Add($"REPLACE({pathExpr}, '\\', '/') LIKE {name} ESCAPE '\\'");
            }
            return string.Join(" OR ", conds);
        }

        private static List<string> SplitPatterns(string? patterns)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(patterns)) return list;
            var parts = patterns.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                list.Add(part.Replace('\\', '/'));
            }
            return list;
        }

        private static string NormalizeGlobToLike(string pattern)
        {
            // Convert simple glob (*, ?) to SQL LIKE with % and _; escape % and _ in literals.
            var s = pattern.Replace("\\", "/");
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '*': sb.Append('%'); break;
                    case '?': sb.Append('_'); break;
                    case '%': sb.Append("\\%"); break;
                    case '_': sb.Append("\\_"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }
}
