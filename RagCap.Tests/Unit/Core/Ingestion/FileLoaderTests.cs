using RagCap.Core.Ingestion;
using System.IO;
using Xunit;

namespace RagCap.Tests.Unit.Core.Ingestion
{
    public class FileLoaderTests : IDisposable
    {
        private readonly string _tempDir;

        public FileLoaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void TextFileLoader_ShouldLoadContent()
        {
            var filePath = Path.Combine(_tempDir, "test.txt");
            var content = "This is a test.";
            File.WriteAllText(filePath, content);

            var loader = new TextFileLoader();
            var loadedContent = loader.LoadContent(filePath);

            Assert.Equal(content, loadedContent);
        }

        [Fact]
        public void MarkdownFileLoader_ShouldLoadContent()
        {
            var filePath = Path.Combine(_tempDir, "test.md");
            var content = "# This is a test";
            File.WriteAllText(filePath, content);

            var loader = new MarkdownFileLoader();
            var loadedContent = loader.LoadContent(filePath);

            Assert.Equal(content, loadedContent);
        }
    }
}
