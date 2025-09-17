
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagCap.Core.Capsule;
using RagCap.Core.Search;
using RagCap.Core.Generation;
using RagCap.Core.Embeddings;

namespace RagCap.Server
{
    public class Startup
    {
        private readonly string _capsulePath;

        public Startup(string capsulePath)
        {
            _capsulePath = capsulePath;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<CapsuleManager>(provider => new CapsuleManager(_capsulePath));

            // Resolve embedding provider based on capsule metadata (default to local)
            services.AddSingleton<IEmbeddingProvider>(sp =>
            {
                var capsule = sp.GetRequiredService<CapsuleManager>();
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

            // Hybrid searcher depends on CapsuleManager + IEmbeddingProvider
            services.AddSingleton<ISearcher>(sp =>
                new HybridSearcher(sp.GetRequiredService<CapsuleManager>(), sp.GetRequiredService<IEmbeddingProvider>()));

            // Answer generator using local Ollama (configurable via env); ensures DI construction succeeds
            services.AddSingleton<IAnswerGenerator>(sp =>
            {
                var url = Environment.GetEnvironmentVariable("RAGCAP_OLLAMA_URL") ?? "http://localhost:11434";
                var model = Environment.GetEnvironmentVariable("RAGCAP_OLLAMA_MODEL") ?? "llama3.1";
                return new LocalAnswerGenerator(url, model);
            });

            services.AddRouting();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // Map all RagCap endpoints under classic Startup hosting
                endpoints.MapRagCapEndpoints();
            });
        }
    }
}
