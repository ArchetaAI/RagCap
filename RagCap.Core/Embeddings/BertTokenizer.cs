
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RagCap.Core.Embeddings
{
    public class BertTokenizer : IDisposable
    {
        private readonly Dictionary<string, int> _vocabulary;

        public BertTokenizer(string vocabPath)
        {
            _vocabulary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StreamReader(vocabPath))
            {
                string line;
                int index = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    _vocabulary[line] = index++;
                }
            }
        }

        public List<long> Encode(string text, int maxLength)
        {
            var tokens = Tokenize(text.ToLowerInvariant());
            var truncatedTokens = tokens.Take(maxLength - 2).ToList(); // -2 for [CLS] and [SEP]

            var inputIds = new List<long> { _vocabulary["[CLS]"] };
            inputIds.AddRange(truncatedTokens.Select(t => (long)_vocabulary.GetValueOrDefault(t, _vocabulary["[UNK]"])));
            inputIds.Add(_vocabulary["[SEP]"]);

            return inputIds;
        }

        private IEnumerable<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return tokens;

            var split = text.Split(new[] { ' ', '	', '
', '' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in split)
            {
                tokens.AddRange(WordpieceTokenize(word));
            }

            return tokens;
        }

        private IEnumerable<string> WordpieceTokenize(string word)
        {
            var tokens = new List<string>();
            string remaining = word;

            while (!string.IsNullOrEmpty(remaining))
            {
                var found = false;
                for (int i = remaining.Length; i >= 1; i--)
                {
                    string sub = (remaining.Length == word.Length) ? remaining.Substring(0, i) : "##" + remaining.Substring(0, i);

                    if (_vocabulary.ContainsKey(sub))
                    {
                        tokens.Add(sub);
                        remaining = remaining.Substring(i);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tokens.Add("[UNK]");
                    break;
                }
            }
            return tokens;
        }

        public void Dispose()
        {
            // Nothing to dispose in this implementation
        }
    }
}
