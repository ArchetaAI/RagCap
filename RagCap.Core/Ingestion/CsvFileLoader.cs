using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace RagCap.Core.Ingestion
{
    public class CsvFileLoader : IFileLoader
    {
        public string LoadContent(string filePath)
        {
            var rows = ReadRows(filePath);
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            // Trim cells for cleaner output
            var trimmedRows = rows
                .Select(row => row.Select(cell => cell?.Trim() ?? string.Empty).ToArray())
                .ToList();

            var headers = trimmedRows[0];
            bool useHeaders = trimmedRows.Count > 1 && AreLikelyHeaders(headers);

            var builder = new StringBuilder();
            if (useHeaders)
            {
                var headerLine = string.Join(", ",
                    headers.Where(h => !string.IsNullOrWhiteSpace(h)));
                if (!string.IsNullOrWhiteSpace(headerLine))
                {
                    builder.AppendLine(headerLine);
                }
            }

            int startIndex = useHeaders ? 1 : 0;
            for (int i = startIndex; i < trimmedRows.Count; i++)
            {
                var row = trimmedRows[i];
                if (useHeaders && headers.Length == row.Length)
                {
                    var pairs = headers.Zip(row, (header, value) => new { header, value })
                        .Where(p => !string.IsNullOrWhiteSpace(p.value))
                        .Select(p => $"{p.header}: {p.value}")
                        .ToList();

                    if (pairs.Count > 0)
                    {
                        builder.AppendLine(string.Join(", ", pairs));
                        continue;
                    }
                }

                var plain = string.Join(", ", row.Where(value => !string.IsNullOrWhiteSpace(value)));
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    builder.AppendLine(plain);
                }
            }

            return builder.ToString().Trim();
        }

        private static List<string[]> ReadRows(string filePath)
        {
            var rows = new List<string[]>();

            using var parser = new TextFieldParser(filePath)
            {
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };

            parser.SetDelimiters(",", ";", "\t");

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields != null)
                {
                    rows.Add(fields);
                }
            }

            return rows;
        }

        private static bool AreLikelyHeaders(IReadOnlyList<string> headers)
        {
            if (headers.Count == 0)
            {
                return false;
            }

            int textualFields = headers.Count(h => h.Any(char.IsLetter));
            int numericFields = headers.Count(h => double.TryParse(h, NumberStyles.Any, CultureInfo.InvariantCulture, out _));
            int distinctCount = headers
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.ToLowerInvariant())
                .Distinct()
                .Count();

            return textualFields >= Math.Max(1, headers.Count / 2)
                   && numericFields < headers.Count
                   && distinctCount == headers.Count;
        }
    }
}
