namespace Ragcap.Core.Loaders
{
    public interface IFileLoader
    {
        bool CanLoad(string extension);
                Task<string> LoadAsync(string filePath);
    }
}
