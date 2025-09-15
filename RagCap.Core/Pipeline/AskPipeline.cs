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

    public async Task<(string answer, List<Chunk> sources)> ExecuteAsync(string question, int topK, string provider, string model, string apiKey, string searchStrategy, string? apiVersion = null, string? endpoint = null)
    {
        using var capsule = new CapsuleManager(_capsulePath);

        // 1. Create Embedding Provider from capsule metadata
        var embeddingProviderName = await capsule.GetMetaValueAsync("embedding_provider") ?? "local";
        var embeddingModel = await capsule.GetMetaValueAsync("embedding_model");
        var embeddingApiVersion = await capsule.GetMetaValueAsync("embedding_api_version");
        var embeddingEndpoint = await capsule.GetMetaValueAsync("embedding_endpoint");

        IEmbeddingProvider embeddingProvider;
        if (embeddingProviderName.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key must be provided for the 'api' provider via --api-key or RAGCAP_API_KEY environment variable.");
            }
            embeddingProvider = new ApiEmbeddingProvider(apiKey, embeddingModel, embeddingEndpoint, embeddingApiVersion);
        }
        else
        {
            embeddingProvider = new LocalEmbeddingProvider();
        }

        // 2. Retrieve chunks
        ISearcher searcher;
        switch (searchStrategy.ToLowerInvariant())
        {
            case "vector":
                searcher = new VectorSearcher(capsule, embeddingProvider);
                break;
            case "bm25":
                searcher = new BM25Searcher(capsule);
                break;
            case "hybrid":
            default:
                searcher = new HybridSearcher(capsule, embeddingProvider);
                break;
        }

        var results = await searcher.SearchAsync(question, topK);

        if (!results.Any())
        {
            return ("No relevant context found.", new List<Chunk>());
        }

        // 3. Select Answer Generator
        IAnswerGenerator answerGenerator;
        if (provider.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key must be provided for the 'api' provider via --api-key or RAGCAP_API_KEY environment variable.");
            }
            answerGenerator = new ApiAnswerGenerator(new HttpClient(), apiKey, model, endpoint, apiVersion);
        }
        else
        {
            answerGenerator = new LocalAnswerGenerator("http://localhost:11434", "llama2");
        }

        // 4. Generate Answer
        var context = results.Select(r => r.Text ?? "");
        var answer = await answerGenerator.GenerateAsync(question, context);

        var chunks = results.Select(r => new Chunk { SourceDocumentId = r.Source, Content = r.Text, Score = r.Score }).ToList();

        return (answer, chunks);
    }
}