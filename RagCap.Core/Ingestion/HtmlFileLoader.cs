namespace RagCap.Core.Ingestion
{
    public class HtmlFileLoader : IFileLoader
    {
        public string LoadContent(string filePath)
        {
            // This is a placeholder implementation. A proper implementation would use a library to parse HTML and extract the main content.
            return System.IO.File.ReadAllText(filePath);
        }
    }
}