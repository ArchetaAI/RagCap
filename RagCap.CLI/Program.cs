using RagCap.CLI.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<AskCommand>("ask");
    config.AddCommand<BuildCommand>("build");
    config.AddCommand<ChunkCommand>("chunk");
    config.AddCommand<DiffCommand>("diff");
    config.AddCommand<EmbedCommand>("embed");
    config.AddCommand<ExportCommand>("export");
    config.AddCommand<InspectCommand>("inspect");
    config.AddCommand<DebugChunkCommand>("debug-chunk");
    config.AddCommand<SearchCommand>("search");
    config.AddCommand<ServeCommand>("serve");
    config.AddCommand<VerifyCommand>("verify");
    config.AddCommand<VerifyVssCommand>("verify-vss");
    config.AddCommand<VerifyVecCommand>("verify-vec");
    config.AddCommand<IndexCommand>("index");
});

return await app.RunAsync(args);
