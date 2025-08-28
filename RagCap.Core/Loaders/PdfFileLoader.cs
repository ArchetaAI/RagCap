using System.IO;
using UglyToad.PdfPig;

namespace Ragcap.Core.Loaders
{
    public class PdfFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

                public Task<string> LoadAsync(string filePath)
        {
            return Task.Run(() =>
            {
                using var doc = PdfDocument.Open(filePath);
                return string.Join("\n", doc.GetPages().Select(p => p.Text));
            });
        }
    }
}
