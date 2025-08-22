using System.IO;
using HtmlAgilityPack;

namespace Ragcap.Core.Loaders
{
    public class HtmlFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".html", StringComparison.OrdinalIgnoreCase) 
                                                || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);

        public string LoadContent(string filePath)
        {
            var doc = new HtmlDocument();
            doc.Load(filePath);
            return doc.DocumentNode.InnerText;
        }
    }
}
