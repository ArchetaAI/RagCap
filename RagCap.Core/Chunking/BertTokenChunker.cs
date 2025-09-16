using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RagCap.Core.Chunking
{
    public class BertTokenChunker
    {
        private readonly int _maxTokens;
        private readonly int _overlapTokens;
        private readonly bool _preserveParagraphs;
        private readonly Tokenizer _whitespaceTokenizer;
        private readonly BertTokenizer _bert;

        public BertTokenChunker(int maxTokens = 200, int overlapTokens = 40, bool preserveParagraphs = true)
        {
            _maxTokens = maxTokens;
            _overlapTokens = Math.Min(overlapTokens, Math.Max(0, maxTokens - 1));
            _preserveParagraphs = preserveParagraphs;
            _whitespaceTokenizer = new Tokenizer();
            _bert = new BertTokenizer(ResolveVocabPath());
        }

        private static string ResolveVocabPath()
        {
            // Mirror LocalEmbeddingProvider's model layout
            var assemblyLocation = Path.GetDirectoryName(typeof(LocalEmbeddingProvider).Assembly.Location) ?? string.Empty;
            var vocabPath = Path.Combine(assemblyLocation, "models", "all-MiniLM-L6-v2", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                // Fallback to current working directory (developer env)
                var alt = Path.Combine(AppContext.BaseDirectory, "models", "all-MiniLM-L6-v2", "vocab.txt");
                if (File.Exists(alt)) return alt;
                throw new FileNotFoundException($"BERT vocab not found at '{vocabPath}'. Ensure models are deployed.");
            }
            return vocabPath;
        }

        public List<Chunk> Chunk(SourceDocument document)
        {
            var chunks = new List<Chunk>();
            var text = document.Content;
            if (string.IsNullOrWhiteSpace(text) || document.Id is null)
            {
                return chunks;
            }

            if (_preserveParagraphs)
            {
                var paragraphs = Regex.Split(text, "(\r\n|\n|\r){2,}")
                                      .Where(p => !string.IsNullOrWhiteSpace(p))
                                      .ToList();
                foreach (var paragraph in paragraphs)
                {
                    AddChunksFromText(paragraph, document.Id, chunks);
                }
            }
            else
            {
                AddChunksFromText(text, document.Id, chunks);
            }

            return chunks;
        }

        private void AddChunksFromText(string text, string documentId, List<Chunk> output)
        {
            var tokens = _whitespaceTokenizer.GetTokens(text); // tokens include trailing whitespace segments
            if (tokens.Count == 0)
            {
                return;
            }

            // Precompute wordpiece counts per token
            var wpCounts = new int[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                wpCounts[i] = string.IsNullOrWhiteSpace(t) ? 0 : CountWordpieces(t);
            }

            int start = 0;
            int n = tokens.Count;
            while (start < n)
            {
                int wp = 0;
                int end = start;
                bool hadNonWhitespace = false;
                while (end < n && (wp + wpCounts[end] <= _maxTokens || !hadNonWhitespace))
                {
                    wp += wpCounts[end];
                    if (wpCounts[end] > 0) hadNonWhitespace = true;
                    end++;
                    if (wp >= _maxTokens && hadNonWhitespace) break;
                }

                var chunkTokens = tokens.GetRange(start, Math.Max(1, end - start));
                var chunkText = string.Join(string.Empty, chunkTokens);
                var chunkWp = wpCounts.Skip(start).Take(Math.Max(1, end - start)).Sum();

                output.Add(new Chunk
                {
                    SourceDocumentId = documentId,
                    Content = chunkText,
                    TokenCount = chunkWp
                });

                if (end >= n) break;

                // Advance start by (maxTokens - overlap) wordpieces
                int toAdvanceWp = Math.Max(1, _maxTokens - _overlapTokens);
                int advanced = 0;
                int newStart = start;
                while (newStart < end && advanced < toAdvanceWp)
                {
                    advanced += wpCounts[newStart];
                    newStart++;
                }
                start = newStart;
            }
        }

        private int CountWordpieces(string token)
        {
            // Tokenize single token via the BERT tokenizer's wordpiece algorithm
            // Encode() adds CLS/SEP, so we approximate by tokenizing the whole token and counting pieces.
            var ids = _bert.Encode(token, int.MaxValue);
            // Remove [CLS] and [SEP]
            return Math.Max(0, ids.Count - 2);
        }
    }
}
