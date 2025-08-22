using System.IO;

namespace Ragcap.Core.Loaders
{
    public class TextFileLoader : IFileLoader
    {
        public bool CanLoad(string extension) => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

        public string LoadContent(string filePath) => File.ReadAllText(filePath);
    }
}
