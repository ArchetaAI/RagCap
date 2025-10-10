using RagCap.Core.Capsule;
using RagCap.Core.Processing;
using Xunit;

namespace RagCap.Tests.Unit.Core.Processing
{
    public class PreprocessorTests
    {
        [Fact]
        public void Process_ShouldRemoveBoilerplate()
        {
            var preprocessor = new Preprocessor(true, true, true, true);
            var html = "<html><head><title>Test</title></head><body><header>hdr</header><p>This is the content.</p><footer>ftr</footer></body></html>";
            var doc = new SourceDocument
            {
                Path = "test.html",
                Hash = "h",
                Content = html,
                DocumentType = "html"
            };
            var expected = "This is the content.";

            var processedContent = preprocessor.Process(doc);

            Assert.Equal(expected, processedContent);
        }
    }
}
