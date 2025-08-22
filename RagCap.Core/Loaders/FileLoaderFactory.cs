using System.Collections.Generic;

namespace Ragcap.Core.Loaders
{
    public static class FileLoaderFactory
    {
        private static readonly List<IFileLoader> Loaders = new()
        {
            new TextFileLoader(),
            new MarkdownFileLoader(),
            new PdfFileLoader(),
            new HtmlFileLoader()
        };

        public static IFileLoader? GetLoader(string extension)
        {
            return Loaders.FirstOrDefault(l => l.CanLoad(extension));
        }
    }
}
