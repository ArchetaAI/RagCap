using System.IO;
using Markdig;

namespace Ragcap.Core.Loaders
{
    public class MarkdownFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".md", StringComparison.OrdinalIgnoreCase);

                public async Task<string> LoadAsync(string filePath)
        {
            var content = await File.ReadAllTextAsync(filePath);
            return Markdown.ToPlainText(content);
        }
    }
}
