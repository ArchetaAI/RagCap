using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagCap.Core.Capsule;
using RagCap.Core.Search;
using RagCap.Core.Generation;
using RagCap.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<CapsuleManager>(provider => new CapsuleManager("test.ragcap"));
builder.Services.AddSingleton<ISearcher, HybridSearcher>();
builder.Services.AddSingleton<IAnswerGenerator, LocalAnswerGenerator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapRagCapEndpoints();

app.Run();