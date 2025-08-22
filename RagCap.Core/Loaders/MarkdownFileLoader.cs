using System.IO;
using Markdig;

namespace Ragcap.Core.Loaders
{
    public class MarkdownFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".md", StringComparison.OrdinalIgnoreCase);

        public string LoadContent(string filePath)
        {
            var content = File.ReadAllText(filePath);
            return Markdown.ToPlainText(content);
        }
    }
}
