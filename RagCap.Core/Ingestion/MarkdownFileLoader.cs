namespace RagCap.Core.Ingestion
{
    public class MarkdownFileLoader : IFileLoader
    {
        public string LoadContent(string filePath)
        {
            return System.IO.File.ReadAllText(filePath);
        }
    }
}