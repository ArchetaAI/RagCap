
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RagCap.Core.Capsule;
using RagCap.Core.Search;
using RagCap.Core.Generation;
using System.Text.Json;

namespace RagCap.Server
{
    public static class Endpoints
    {
        public static void MapRagCapEndpoints(this WebApplication app)
        {
            app.MapGet("/search", async (string query, int? topK, ILogger<Program> logger, CapsuleManager capsule, ISearcher searcher) =>
            {
                logger.LogInformation("Searching for '{query}' with topK={topK}", query, topK);
                var results = await searcher.SearchAsync(query, topK ?? 10);
                return Results.Ok(results);
            });

            app.MapGet("/ask", async (string query, int? topK, ILogger<Program> logger, ISearcher searcher, IAnswerGenerator answerGenerator) =>
            {
                logger.LogInformation("Asking '{query}' with topK={topK}", query, topK);
                var searchResults = await searcher.SearchAsync(query, topK ?? 10);
                var context = searchResults.Select(r => r.Text!);
                var answer = await answerGenerator.GenerateAsync(query, context);
                return Results.Ok(answer);
            });

            app.MapGet("/chunk/{id}", async (int id, ILogger<Program> logger, CapsuleManager capsule) =>
            {
                logger.LogInformation("Getting chunk with id={id}", id);
                var chunk = await capsule.GetChunkAsync(id);
                return chunk is not null ? Results.Ok(chunk) : Results.NotFound();
            });

            app.MapGet("/source/{id}", async (int id, ILogger<Program> logger, CapsuleManager capsule) =>
            {
                logger.LogInformation("Getting source with id={id}", id);
                var source = await capsule.GetSourceDocumentAsync(id);
                return source is not null ? Results.Ok(source) : Results.NotFound();
            });
        }
    }
}
