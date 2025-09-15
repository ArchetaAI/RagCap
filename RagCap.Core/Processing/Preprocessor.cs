using LanguageDetection;
using RagCap.Core.Capsule;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RagCap.Core.Processing
{
    public class Preprocessor
    {
        private List<string> _codeBlocks = new List<string>();
        private readonly LanguageDetector _languageDetector;
        private readonly bool _removeBoilerplate;
        private readonly bool _preserveCode;
        private readonly bool _flattenTables;
        private readonly bool _detectLanguage;

        public Preprocessor(bool removeBoilerplate = true, bool preserveCode = true, bool flattenTables = true, bool detectLanguage = true)
        {
            _languageDetector = new LanguageDetector();
            _languageDetector.AddAllLanguages();
            _removeBoilerplate = removeBoilerplate;
            _preserveCode = preserveCode;
            _flattenTables = flattenTables;
            _detectLanguage = detectLanguage;
        }

        public string Process(SourceDocument document)
        {
            var text = document.Content;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            if (_detectLanguage)
            {
                DetectLanguage(document, text);
            }

            if (_preserveCode)
            {
                text = PreserveCodeFences(text, document.DocumentType ?? string.Empty);
            }

            if (_removeBoilerplate && document.DocumentType == "html")
            {
                text = RemoveBoilerplate(text);
            }

            if (_flattenTables)
            {
                if (document.DocumentType == "html")
                {
                    text = FlattenHtmlTables(text);
                }
                else if (document.DocumentType == "markdown" || document.DocumentType == "md")
                {
                    text = FlattenMarkdownTables(text);
                }
            }

            // normalize whitespace while preserving paragraph boundaries
            // 1) normalize line endings to \n
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            // 2) collapse spaces/tabs/formfeeds/vertical tabs but keep newlines
            text = Regex.Replace(text, "[ \t\f\v]+", " ");
            // 3) trim trailing spaces at end of lines
            text = Regex.Replace(text, "[ \t]+\n", "\n");
            // 4) collapse 3+ newlines to exactly 2 so paragraph detection remains effective
            text = Regex.Replace(text, "\n{3,}", "\n\n");
            text = text.Trim();

            if (_preserveCode)
            {
                text = RestoreCodeFences(text);
            }

            return text;
        }

        private void DetectLanguage(SourceDocument document, string text)
        {
            var language = _languageDetector.Detect(text);
            if (language != null)
            {
                document.Metadata["language"] = language;
            }
        }

        private string PreserveCodeFences(string text, string documentType)
        {
            _codeBlocks.Clear();
            if (documentType == "markdown" || documentType == "md")
            {
                text = Regex.Replace(text, "```(.*?)```", match =>
                {
                    _codeBlocks.Add(match.Value);
                    return $"<codeblock>{_codeBlocks.Count - 1}</codeblock>";
                }, RegexOptions.Singleline);
            }
            else if (documentType == "html")
            {
                text = Regex.Replace(text, "<pre><code.*?>.*?</code></pre>", match =>
                {
                    _codeBlocks.Add(match.Value);
                    return $"<codeblock>{_codeBlocks.Count - 1}</codeblock>";
                }, RegexOptions.Singleline);
            }
            return text;
        }

        private string RestoreCodeFences(string text)
        {
            text = Regex.Replace(text, "<codeblock>(.*?)</codeblock>", match =>
            {
                int index = int.Parse(match.Groups[1].Value);
                return _codeBlocks[index];
            });
            return text;
        }

        private string RemoveBoilerplate(string text)
        {
            // remove headers, footers, and navbars
            text = Regex.Replace(text, @"<header.*?>.*?</header>", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<footer.*?>.*?</footer>", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<nav.*?>.*?</nav>", "", RegexOptions.Singleline);
            return text;
        }

        private string FlattenHtmlTables(string text)
        {
            return Regex.Replace(text, "<table.*?>(.*?)</table>", match =>
            {
                var tableContent = match.Groups[1].Value;
                var headers = new List<string>();
                var rows = new List<List<string>>();

                // extract headers
                var thMatches = Regex.Matches(tableContent, "<th.*?>(.*?)</th>");
                foreach (Match thMatch in thMatches)
                {
                    headers.Add(thMatch.Groups[1].Value.Trim());
                }

                // extract rows
                var trMatches = Regex.Matches(tableContent, "<tr.*?>(.*?)</tr>");
                foreach (Match trMatch in trMatches)
                {
                    var rowContent = trMatch.Groups[1].Value;
                    var cells = new List<string>();
                    var tdMatches = Regex.Matches(rowContent, "<td.*?>(.*?)</td>");
                    foreach (Match tdMatch in tdMatches)
                    {
                        cells.Add(tdMatch.Groups[1].Value.Trim());
                    }
                    if (cells.Count > 0)
                    {
                        rows.Add(cells);
                    }
                }

                // linearize
                var linearizedRows = new List<string>();
                foreach (var row in rows)
                {
                    var linearizedRow = new List<string>();
                    for (int i = 0; i < headers.Count && i < row.Count; i++)
                    {
                        linearizedRow.Add($"{headers[i]}: {row[i]}");
                    }
                    linearizedRows.Add(string.Join(", ", linearizedRow));
                }

                return string.Join("\n", linearizedRows);
            }, RegexOptions.Singleline);
        }

        private string FlattenMarkdownTables(string text)
        {
            var lines = text.Split('\n');
            var newLines = new List<string>();
            bool inTable = false;
            List<string>? headers = null;

            foreach (var line in lines)
            {
                if (line.Contains("|") && line.Contains("-"))
                {
                    // header separator line
                    inTable = true;
                    var headerLine = newLines.Last();
                    headers = headerLine.Split('|').Select(h => h.Trim()).Where(h => !string.IsNullOrEmpty(h)).ToList();
                    newLines.RemoveAt(newLines.Count - 1); // remove header line from output
                }
                else if (inTable && line.Contains("|"))
                {
                    // table row
                    var cells = line.Split('|').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
                    if (headers != null && cells.Count == headers.Count)
                    {
                        var linearizedRow = new List<string>();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            linearizedRow.Add($"{headers[i]}: {cells[i]}");
                        }
                        newLines.Add(string.Join(", ", linearizedRow));
                    }
                }
                else
                {
                    inTable = false;
                    headers = null;
                    newLines.Add(line);
                }
            }
            return string.Join("\n", newLines);
        }
    }
}
