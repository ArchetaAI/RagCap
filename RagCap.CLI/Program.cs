using System.CommandLine;
using RagCap.CLI.Commands;

var root = new RootCommand("RagCap CLI");


root.AddCommand(VerifyCommand.Create());
root.AddCommand(ChunkCommand.Create());
root.AddCommand(BuildCommand.Create());
root.AddCommand(EmbedCommand.Create());
root.AddCommand(InspectCommand.Create());


return await root.InvokeAsync(args);
