
using System.Threading;
using System.Threading.Tasks;

namespace RagCap.Core.Embeddings
{
    public class LocalEmbeddingProvider : IEmbeddingProvider
    {
        private readonly LocalEmbeddingService _embeddingService;

        public LocalEmbeddingProvider()
        {
            // Assuming the model and vocab files are in the Resources/Models directory
            string modelPath = "Resources/Models/model.onnx";
            string vocabPath = "Resources/Models/vocab.txt";
            _embeddingService = new LocalEmbeddingService(modelPath, vocabPath);
        }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_embeddingService.GenerateEmbedding(text));
        }
    }
}
