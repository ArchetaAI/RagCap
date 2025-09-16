
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
            var assemblyLocation = AppContext.BaseDirectory ?? string.Empty;
            var modelPath = Path.Combine(assemblyLocation, "models", "all-MiniLM-L6-v2", "model.onnx");
            var vocabPath = Path.Combine(assemblyLocation, "models", "all-MiniLM-L6-v2", "vocab.txt");
            if (!File.Exists(modelPath) || !File.Exists(vocabPath))
            {
                var msg = $"Local embedding model files not found. Expected: {modelPath} and {vocabPath}. " +
                          "Ensure models are packaged alongside the CLI or set provider=api in the recipe.";
                throw new FileNotFoundException(msg);
            }
            _embeddingService = new LocalEmbeddingService(modelPath, vocabPath);
        }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_embeddingService.GenerateEmbedding(text));
        }
    }
}
