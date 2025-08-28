namespace RagCap.Core.Capsule
{
    public class Chunk
    {
                public long Id { get; set; }
        public string? SourceDocumentId { get; set; }
        public string? Content { get; set; }
        public int TokenCount { get; set; }
    }
}