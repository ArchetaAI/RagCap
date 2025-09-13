using Moq;
using RagCap.Core.Embeddings;
using System.Threading.Tasks;
using Xunit;

namespace RagCap.Tests.Unit.Core.Embeddings
{
    public class EmbeddingProviderTests
    {
        [Fact]
        public async Task LocalEmbeddingProvider_ShouldReturnVectorOfCorrectLength()
        {
            var provider = new LocalEmbeddingProvider();
            var embedding = await provider.GenerateEmbeddingAsync("This is a test.");

            Assert.Equal(384, embedding.Length);
        }

        [Fact]
        public async Task ApiEmbeddingProvider_ShouldHandleMissingApiKey()
        {
            var model = "text-embedding-ada-002";
            var apiKey = "";

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => new ApiEmbeddingProvider(model, apiKey).GenerateEmbeddingAsync("test"));
            Assert.Equal("API key cannot be null or empty. (Parameter 'apiKey')", exception.Message);
        }
    }
}
