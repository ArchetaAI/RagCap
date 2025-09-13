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
            var content = "<html><head><title>Test</title></head><body><p>This is the content.</p></body></html>";
            var expected = "This is the content.";

            var processedContent = preprocessor.Process(content);

            Assert.Equal(expected, processedContent);
        }
    }
}
