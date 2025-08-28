using System.IO;
using HtmlAgilityPack;

namespace Ragcap.Core.Loaders
{
    public class HtmlFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".html", StringComparison.OrdinalIgnoreCase) 
                                                || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);

                public async Task<string> LoadAsync(string filePath)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await File.ReadAllTextAsync(filePath));
            return doc.DocumentNode.InnerText;
        }
    }
}
