using System.IO;

namespace Ragcap.Core.Loaders
{
    public class TextFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

                public async Task<string> LoadAsync(string filePath) => await File.ReadAllTextAsync(filePath);
    }
}
