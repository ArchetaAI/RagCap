using System.Collections.Generic;

namespace RagCap.CLI.Commands
{
    public class Recipe
    {
        public List<Source> Sources { get; set; }
        public Chunking Chunking { get; set; }
        public Embeddings Embeddings { get; set; }
        public Preprocess Preprocess { get; set; }
        public Output Output { get; set; }
    }

    public class Source
    {
        public string Path { get; set; }
        public string Type { get; set; }
    }

    public class Chunking
    {
        public int Size { get; set; }
        public int Overlap { get; set; }
    }

    public class Embeddings
    {
        public string Provider { get; set; }
        public string Model { get; set; }
        public int Dimension { get; set; }
    }

    public class Preprocess
    {
        public bool Boilerplate { get; set; }
        public bool Preserve_code { get; set; }
        public bool Flatten_tables { get; set; }
        public bool Detect_language { get; set; }
    }

    public class Output
    {
        public string Path { get; set; }
    }
}
