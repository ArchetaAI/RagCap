
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagCap.Core.Capsule;
using RagCap.Core.Search;
using RagCap.Core.Generation;

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
            services.AddSingleton<ISearcher, HybridSearcher>();
            services.AddSingleton<IAnswerGenerator, LocalAnswerGenerator>();
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
                if (app is WebApplication webApp)
                {
                    webApp.MapRagCapEndpoints();
                }
            });
        }
    }
}
