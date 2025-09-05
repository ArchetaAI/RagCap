
using System;
using System.Linq;

namespace RagCap.Core.Search
{
    public static class SimilarityMetrics
    {
        public static float CosineSimilarity(float[] vec1, float[] vec2)
        {
            if (vec1 == null || vec2 == null || vec1.Length != vec2.Length)
            {
                throw new ArgumentException("Vectors must be non-null and have the same length.");
            }

            var dotProduct = vec1.Zip(vec2, (a, b) => a * b).Sum();
            var norm1 = Math.Sqrt(vec1.Sum(x => x * x));
            var norm2 = Math.Sqrt(vec2.Sum(x => x * x));

            if (norm1 == 0 || norm2 == 0)
                return 0;

            return (float)(dotProduct / (norm1 * norm2));
        }
    }
}
