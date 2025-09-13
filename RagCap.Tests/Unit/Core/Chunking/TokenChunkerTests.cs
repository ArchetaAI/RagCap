using RagCap.Core.Chunking;
using RagCap.Core.Capsule;
using Xunit;

namespace RagCap.Tests.Unit.Core.Chunking
{
    public class TokenChunkerTests
    {
        [Fact]
        public void Chunk_ShouldSplitTextIntoChunksOfCorrectSize()
        {
            var chunker = new TokenChunker(10, 2);
            var document = new SourceDocument { Content = "This is a test sentence for the chunker." };

            var chunks = chunker.Chunk(document);

            Assert.All(chunks, c => Assert.True(c.Content.Split(' ').Length <= 10));
        }

        [Fact]
        public void Chunk_ShouldHandleOverlapCorrectly()
        {
            var chunker = new TokenChunker(10, 2);
            var document = new SourceDocument { Content = "one two three four five six seven eight nine ten eleven twelve" };

            var chunks = chunker.Chunk(document);

            Assert.Equal("one two three four five six seven eight nine ten", chunks[0].Content);
            Assert.Equal("nine ten eleven twelve", chunks[1].Content);
        }
    }
}
