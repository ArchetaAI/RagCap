using System.Collections.Generic;

namespace RagCap.Core.Recipes
{
    public class Recipe
    {
        public List<Source> Sources { get; set; } = new List<Source>();
        public Chunking Chunking { get; set; } = new Chunking();
        public Embeddings Embeddings { get; set; } = new Embeddings();
        public Preprocess Preprocess { get; set; } = new Preprocess();
        public Output Output { get; set; } = new Output();
    }

    public class Source
    {
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class Chunking
    {
        public int Size { get; set; }
        public int Overlap { get; set; }
    }

    public class Embeddings
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Dimension { get; set; }
        public string? ApiVersion { get; set; }
        public string? Endpoint { get; set; }
    }

    public class Preprocess
    {
        public bool Boilerplate { get; set; }
        public bool PreserveCode { get; set; }
        public bool FlattenTables { get; set; }
        public bool DetectLanguage { get; set; }
    }

    public class Output
    {
        public string Path { get; set; } = string.Empty;
    }
}
