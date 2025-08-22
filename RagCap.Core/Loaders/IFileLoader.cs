namespace Ragcap.Core.Loaders
{
    public interface IFileLoader
    {
        bool CanLoad(string extension);
        string LoadContent(string filePath);
    }
}
