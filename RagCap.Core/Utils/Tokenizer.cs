
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RagCap.Core.Utils
{
    public class Tokenizer
    {
        // A simple regex for tokenization. This can be improved later.
        private static readonly Regex _wordRegex = new Regex(@"\w+|[^\w\s]", RegexOptions.Compiled);

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            return _wordRegex.Matches(text).Count;
        }

        public List<string> GetTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }
            var tokens = new List<string>();
            foreach (Match match in _wordRegex.Matches(text))
            {
                tokens.Add(match.Value);
            }
            return tokens;
        }
    }
}
