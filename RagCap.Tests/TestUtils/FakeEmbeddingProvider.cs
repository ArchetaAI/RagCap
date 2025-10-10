using RagCap.Core.Embeddings;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RagCap.Tests.TestUtils
{
    // Deterministic, lightweight embedding provider for tests
    public class FakeEmbeddingProvider : IEmbeddingProvider
    {
        private readonly int _dim;
        public FakeEmbeddingProvider(int dim = 32)
        {
            _dim = dim;
        }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            // Simple hash-based embedding; stable across runs
            unchecked
            {
                var vec = new float[_dim];
                int h = 17;
                foreach (var ch in text ?? string.Empty)
                {
                    h = h * 31 + ch;
                    int idx = Math.Abs(h % _dim);
                    vec[idx] += ((ch % 97) / 96.0f);
                }
                // L2 normalize to match cosine usage
                var norm = (float)Math.Sqrt(vec.Sum(v => v * v)) + 1e-6f;
                for (int i = 0; i < vec.Length; i++) vec[i] /= norm;
                return Task.FromResult(vec);
            }
        }
    }
}

