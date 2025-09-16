using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RagCap.Core.Capsule;
using RagCap.Core.Search;
using RagCap.Core.Generation;
using RagCap.Core.Embeddings;
using RagCap.Server;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var capsulePath = builder.Configuration["RagCap:CapsulePath"] ?? "local_capsule.ragcap";

// Services
builder.Services.AddSingleton(new CapsuleManager(capsulePath));
builder.Services.AddSingleton<IEmbeddingProvider>(sp =>
{
    var capsule = sp.GetRequiredService<CapsuleManager>();
    // Resolve provider from capsule meta; default to local
    var provider = capsule.GetMetaValueAsync("embedding_provider").GetAwaiter().GetResult() ?? "local";
    if (string.Equals(provider, "api", StringComparison.OrdinalIgnoreCase))
    {
        var model = capsule.GetMetaValueAsync("embedding_model").GetAwaiter().GetResult();
        var endpoint = capsule.GetMetaValueAsync("embedding_endpoint").GetAwaiter().GetResult();
        var apiVersion = capsule.GetMetaValueAsync("embedding_api_version").GetAwaiter().GetResult();
        var apiKey = Environment.GetEnvironmentVariable("RAGCAP_API_KEY") ?? string.Empty;
        return new ApiEmbeddingProvider(apiKey, model, endpoint, apiVersion);
    }
    return new LocalEmbeddingProvider();
});
builder.Services.AddSingleton<ISearcher>(sp =>
    new HybridSearcher(sp.GetRequiredService<CapsuleManager>(), sp.GetRequiredService<IEmbeddingProvider>()));
builder.Services.AddSingleton<IAnswerGenerator, LocalAnswerGenerator>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.MapRagCapEndpoints();

app.Run();
