using System.CommandLine;
using RagCap.CLI.Commands;

var root = new RootCommand("RagCap CLI");

root.AddCommand(VerifyCommand.Create());

return await root.InvokeAsync(args);
