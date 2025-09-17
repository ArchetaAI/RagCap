using System.Collections.Generic;

namespace RagCap.Core.Pipeline
{
    public class DiffValue<T>
    {
        public T Value1 { get; set; }
        public T Value2 { get; set; }
    }

    public class DiffResult
    {
        public Dictionary<string, DiffValue<string?>> Manifest { get; set; } = new Dictionary<string, DiffValue<string?>>();
        public List<string> AddedSources { get; set; } = new List<string>();
        public List<string> RemovedSources { get; set; } = new List<string>();
        public List<string> ModifiedSources { get; set; } = new List<string>();
        public DiffValue<int> ChunkCount { get; set; } = new DiffValue<int>();
        public DiffValue<double> AverageChunkSize { get; set; } = new DiffValue<double>();
        public DiffValue<int> EmbeddingDimensions { get; set; } = new DiffValue<int>();
        public DiffValue<string?> Recipe { get; set; } = new DiffValue<string?>();
    }
}