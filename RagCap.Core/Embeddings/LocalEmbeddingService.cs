
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RagCap.Core.Embeddings
{
    public class LocalEmbeddingService : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly BertTokenizer _tokenizer;
        private const int MaxLength = 256;

        public LocalEmbeddingService(string modelPath, string vocabPath)
        {
            _session = new InferenceSession(modelPath);
            _tokenizer = new BertTokenizer(vocabPath);
        }

        public float[] GenerateEmbedding(string text)
        {
            var inputIds = _tokenizer.Encode(text, MaxLength);
            var attentionMask = Enumerable.Repeat(1L, inputIds.Count).ToList();
            var tokenTypeIds = Enumerable.Repeat(0L, inputIds.Count).ToList();

            // Pad sequences to MaxLength
            var paddedInputIds = Pad(inputIds, MaxLength, 0L);
            var paddedAttentionMask = Pad(attentionMask, MaxLength, 0L);
            var paddedTokenTypeIds = Pad(tokenTypeIds, MaxLength, 0L);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(paddedInputIds.ToArray(), new[] { 1, MaxLength })),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(paddedAttentionMask.ToArray(), new[] { 1, MaxLength })),
                NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(paddedTokenTypeIds.ToArray(), new[] { 1, MaxLength }))
            };

            using var results = _session.Run(inputs);
            var lastHiddenState = results.First().AsTensor<float>();

            var embedding = MeanPooling(lastHiddenState, paddedAttentionMask);
            return Normalize(embedding);
        }

        private List<T> Pad<T>(List<T> sequence, int maxLength, T padValue)
        {
            var padded = new List<T>(sequence);
            while (padded.Count < maxLength)
            {
                padded.Add(padValue);
            }
            return padded;
        }

        private float[] MeanPooling(Tensor<float> tokenEmbeddings, List<long> attentionMask)
        {
            var embeddingSum = new float[tokenEmbeddings.Dimensions[2]];
            var tokenCount = 0;

            for (int i = 0; i < tokenEmbeddings.Dimensions[1]; i++)
            {
                if (attentionMask[i] == 1)
                {
                    tokenCount++;
                    for (int j = 0; j < tokenEmbeddings.Dimensions[2]; j++)
                    {
                        embeddingSum[j] += tokenEmbeddings[0, i, j];
                    }
                }
            }

            if (tokenCount == 0) return embeddingSum; // Should not happen with CLS token

            var meanEmbedding = new float[embeddingSum.Length];
            for (int i = 0; i < embeddingSum.Length; i++)
            {
                meanEmbedding[i] = embeddingSum[i] / tokenCount;
            }

            return meanEmbedding;
        }

        private float[] Normalize(float[] v)
        {
            var norm = (float)Math.Sqrt(v.Sum(x => x * x));
            if (norm == 0) return v;
            return v.Select(x => x / norm).ToArray();
        }

        public void Dispose()
        {
            _session?.Dispose();
            _tokenizer?.Dispose();
        }
    }
}
