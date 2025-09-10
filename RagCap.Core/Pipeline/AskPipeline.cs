using RagCap.Core.Capsule;
using RagCap.Core.Embeddings;
using RagCap.Core.Generation;
using RagCap.Core.Search;

namespace RagCap.Core.Pipeline;

public class AskPipeline
{
    private readonly string _capsulePath;

    public AskPipeline(string capsulePath)
    {
        _capsulePath = capsulePath;
    }

    public async Task<(string answer, List<Chunk> sources)> ExecuteAsync(string question, int topK, string provider, string model, string apiKey)
    {
        using var capsule = new CapsuleManager(_capsulePath);

        // 1. Retrieve chunks
        var embeddingProvider = new LocalEmbeddingProvider(); // Assuming local for now
        var searcher = new HybridSearcher(capsule, embeddingProvider);
        var results = await searcher.SearchAsync(question, topK);

        if (!results.Any())
        {
            return ("No relevant context found.", new List<Chunk>());
        }

        // 2. Select Answer Generator
        IAnswerGenerator answerGenerator;
        if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key must be provided for the 'api' provider via --api-key or RAGCAP_API_KEY environment variable.");
            }
            answerGenerator = new ApiAnswerGenerator(new HttpClient(), apiKey, model);
        }
        else
        {
            answerGenerator = new LocalAnswerGenerator("http://localhost:11434", "llama2");
        }

        // 3. Generate Answer
        var context = results.Select(r => r.Text ?? "");
        var answer = await answerGenerator.GenerateAsync(question, context);

        var chunks = results.Select(r => new Chunk { SourceDocumentId = r.Source, Content = r.Text, Score = r.Score }).ToList();

        return (answer, chunks);
    }
}