using RagCap.Core.Loaders;
using System.IO;

namespace RagCap.Core.Ingestion
{
    public static class FileLoaderFactory
    {
        public static IFileLoader GetLoader(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => new TextFileLoader(),
                ".md" => new MarkdownFileLoader(),
                ".pdf" => new PdfFileLoader(),
                ".html" => new HtmlFileLoader(),
                _ => throw new NotSupportedException($"File type {extension} is not supported."),
            };
        }
    }
}