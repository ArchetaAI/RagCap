
using System.Threading.Tasks;

namespace RagCap.Core.Embeddings
{
    public interface IEmbeddingProvider
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
}
