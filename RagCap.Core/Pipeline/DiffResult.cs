using System.Collections.Generic;

namespace RagCap.Core.Pipeline
{
    public class DiffResult
    {
        public Dictionary<string, (string, string)> Manifest { get; set; } = new Dictionary<string, (string, string)>();
        public List<string> AddedSources { get; set; } = new List<string>();
        public List<string> RemovedSources { get; set; } = new List<string>();
        public List<string> ModifiedSources { get; set; } = new List<string>();
        public (int, int) ChunkCount { get; set; }
        public (double, double) AverageChunkSize { get; set; }
        public (int, int) EmbeddingDimensions { get; set; }
        public (string, string) Recipe { get; set; }
    }
}
