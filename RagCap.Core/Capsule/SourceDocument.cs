using System.Collections.Generic;

namespace RagCap.Core.Capsule
{
    public class SourceDocument
    {
        public string? Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? DocumentType { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}