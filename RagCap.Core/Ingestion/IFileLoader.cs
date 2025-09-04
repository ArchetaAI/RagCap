namespace RagCap.Core.Ingestion
{
    public interface IFileLoader
    {
        string LoadContent(string filePath);
    }
}