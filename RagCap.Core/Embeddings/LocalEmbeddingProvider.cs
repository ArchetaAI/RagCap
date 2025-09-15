
using System.IO;
using System.Reflection;
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
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var modelPath = Path.Combine(assemblyLocation, "models", "all-MiniLM-L6-v2", "model.onnx");
            var vocabPath = Path.Combine(assemblyLocation, "models", "all-MiniLM-L6-v2", "vocab.txt");
            _embeddingService = new LocalEmbeddingService(modelPath, vocabPath);
        }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_embeddingService.GenerateEmbedding(text));
        }
    }
}
