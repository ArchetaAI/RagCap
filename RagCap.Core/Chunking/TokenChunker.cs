using RagCap.Core.Capsule;
using RagCap.Core.Processing;
using RagCap.Core.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RagCap.Core.Chunking
{
    public class TokenChunker
    {
        private readonly int _maxTokens;
        private readonly int _overlapTokens;
        private readonly bool _preserveParagraphs;
        private readonly Tokenizer _tokenizer;
        private readonly Preprocessor _preprocessor;

        public TokenChunker(int maxTokens = 512, int overlapTokens = 50, bool preserveParagraphs = true)
        {
            _maxTokens = maxTokens;
            // Ensure overlap is less than maxTokens
            _overlapTokens = Math.Min(overlapTokens, maxTokens / 2);
            _preserveParagraphs = preserveParagraphs;
            _tokenizer = new Tokenizer();
            _preprocessor = new Preprocessor();
        }

        public List<Chunk> Chunk(SourceDocument document)
        {
            var chunks = new List<Chunk>();
            var text = _preprocessor.Process(document);
            if (string.IsNullOrWhiteSpace(text) || document.Id is null)
            {
                return chunks;
            }

            if (_preserveParagraphs)
            {
                var paragraphs = Regex.Split(text, @"(\r\n|\n|\r){2,}")
                                      .Where(p => !string.IsNullOrWhiteSpace(p))
                                      .ToList();
                foreach (var paragraph in paragraphs)
                {
                    var tokenCount = _tokenizer.CountTokens(paragraph);
                    if (tokenCount <= _maxTokens)
                    {
                        chunks.Add(new Chunk
                        {
                            SourceDocumentId = document.Id,
                            Content = paragraph,
                            TokenCount = tokenCount
                        });
                    }
                    else
                    {
                        chunks.AddRange(ChunkText(paragraph, document.Id));
                    }
                }
            }
            else
            {
                chunks.AddRange(ChunkText(text, document.Id));
            }

            return chunks;
        }

        private IEnumerable<Chunk> ChunkText(string text, string documentId)
        {
            var chunks = new List<Chunk>();
            var tokens = _tokenizer.GetTokens(text);
            var tokenCount = tokens.Count;
            var start = 0;

            while (start < tokenCount)
            {
                var end = Math.Min(start + _maxTokens, tokenCount);
                var chunkTokens = tokens.GetRange(start, end - start);
                var chunkText = string.Join("", chunkTokens);

                chunks.Add(new Chunk
                {
                    SourceDocumentId = documentId,
                    Content = chunkText,
                    TokenCount = chunkTokens.Count
                });

                if (end == tokenCount)
                {
                    break;
                }

                start += _maxTokens - _overlapTokens;
            }
            return chunks;
        }
    }
}