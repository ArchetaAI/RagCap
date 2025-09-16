using System.Text;
using HtmlAgilityPack;

namespace RagCap.Core.Ingestion
{
    public class HtmlFileLoader : IFileLoader
    {
        public class HtmlExtractionOptions
        {
            public bool IncludeHeadingContext { get; set; } = true;
            public bool IncludeTitle { get; set; } = true;
        }

        // Global options can be configured by the pipeline before ingestion
        public static HtmlExtractionOptions Options { get; set; } = new HtmlExtractionOptions();

        public string LoadContent(string filePath)
        {
            var html = System.IO.File.ReadAllText(filePath);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove non-content nodes (script/style/etc) and common boilerplate containers
            var removeNodes = doc.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//nav|//footer|//header");
            if (removeNodes != null)
            {
                foreach (var node in removeNodes)
                {
                    node.Remove();
                }
            }

            // Prefer <main> or <article>; fallback to <body>
            var root = doc.DocumentNode.SelectSingleNode("//main")
                       ?? doc.DocumentNode.SelectSingleNode("//article")
                       ?? doc.DocumentNode.SelectSingleNode("//body")
                       ?? doc.DocumentNode;

            var blocks = new List<string>();
            var title = HtmlEntity.DeEntitize((doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty).Trim());
            if (Options.IncludeTitle && !string.IsNullOrWhiteSpace(title))
            {
                blocks.Add(title);
            }

            // Track current heading path (H1 > H2 > H3)
            var headingLevels = new string[6];

            // Helper to get visible text for a node, including image alt text
            string GetInlineText(HtmlNode node)
            {
                if (node.NodeType == HtmlNodeType.Text)
                {
                    var t = HtmlEntity.DeEntitize(node.InnerText);
                    return string.IsNullOrWhiteSpace(t) ? string.Empty : t.Trim();
                }
                if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
                {
                    var alt = node.GetAttributeValue("alt", string.Empty);
                    return string.IsNullOrWhiteSpace(alt) ? string.Empty : $"[Image: {alt.Trim()}]";
                }

                var b = new StringBuilder();
                foreach (var c in node.ChildNodes)
                {
                    var part = GetInlineText(c);
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        if (b.Length > 0) b.Append(' ');
                        b.Append(part);
                    }
                }
                return b.ToString();
            }

            void UpdateHeading(HtmlNode node)
            {
                int level = node.Name.Length == 2 && node.Name[0] == 'h' && char.IsDigit(node.Name[1])
                    ? (int)char.GetNumericValue(node.Name[1])
                    : 0;
                if (level >= 1 && level <= 6)
                {
                    var text = GetInlineText(node);
                    headingLevels[level - 1] = text;
                    for (int i = level; i < 6; i++) headingLevels[i] = null;
                }
            }

            // Add a content block with optional heading context
            void AddBlock(string content)
            {
                if (string.IsNullOrWhiteSpace(content)) return;
                if (Options.IncludeHeadingContext)
                {
                    var context = string.Join(" > ", headingLevels.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(context))
                    {
                        blocks.Add(context);
                    }
                }
                blocks.Add(content.Trim());
            }

            // Handle tables: linearize rows into "Header: Cell" pairs
            void AddTable(HtmlNode table)
            {
                var headers = new List<string>();
                var headerRow = table.SelectSingleNode(".//thead//tr") ?? table.SelectSingleNode(".//tr[th]");
                if (headerRow != null)
                {
                    foreach (var th in headerRow.SelectNodes(".//th") ?? Enumerable.Empty<HtmlNode>())
                    {
                        headers.Add(GetInlineText(th));
                    }
                }

                foreach (var tr in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
                {
                    var cells = tr.SelectNodes(".//td")?.Select(GetInlineText).ToList() ?? new List<string>();
                    if (cells.Count == 0) continue;
                    if (headers.Count == cells.Count && headers.Count > 0)
                    {
                        var pairs = headers.Zip(cells, (h, v) => $"{h}: {v}");
                        AddBlock(string.Join(", ", pairs));
                    }
                    else
                    {
                        AddBlock(string.Join(" | ", cells));
                    }
                }
            }

            // DFS through content root
            void Walk(HtmlNode node)
            {
                var name = node.Name.ToLowerInvariant();
                if (name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                {
                    UpdateHeading(node);
                    return; // headings handled as context
                }

                if (name == "p" || name == "blockquote" || name == "pre")
                {
                    AddBlock(GetInlineText(node));
                    return;
                }

                if (name == "ul" || name == "ol")
                {
                    foreach (var li in node.SelectNodes(".//li") ?? Enumerable.Empty<HtmlNode>())
                    {
                        var liText = GetInlineText(li);
                        if (!string.IsNullOrWhiteSpace(liText)) AddBlock("- " + liText);
                    }
                    return;
                }

                if (name == "figure")
                {
                    var caption = node.SelectSingleNode(".//figcaption");
                    var inner = GetInlineText(node);
                    if (!string.IsNullOrWhiteSpace(caption?.InnerText))
                    {
                        AddBlock($"{inner} — {GetInlineText(caption)}");
                    }
                    else
                    {
                        AddBlock(inner);
                    }
                    return;
                }

                if (name == "table")
                {
                    AddTable(node);
                    return;
                }

                // Recurse children by default
                foreach (var child in node.ChildNodes)
                {
                    Walk(child);
                }
            }

            Walk(root);

            // Join blocks with paragraph separators and normalize whitespace
            var textContent = string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
            textContent = System.Text.RegularExpressions.Regex.Replace(textContent, "[\t\f\v ]+", " ");
            textContent = System.Text.RegularExpressions.Regex.Replace(textContent, "[ ]*\n[ ]*", "\n");
            textContent = System.Text.RegularExpressions.Regex.Replace(textContent, "\n{3,}", "\n\n");
            return textContent.Trim();
        }
    }
}
