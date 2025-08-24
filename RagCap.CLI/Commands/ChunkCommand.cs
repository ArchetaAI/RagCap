
using System.CommandLine;
using RagCap.Core.Chunking;
using RagCap.Core.Capsule;

namespace RagCap.CLI.Commands
{
    public static class ChunkCommand
    {
        public static Command Create()
        {
            var cmd = new Command("chunk", "Chunk a file and print the chunks.");
            var fileArg = new Argument<string>("file", "Path to the file to chunk.");
            var maxTokensOption = new Option<int>("--tokens", () => 512, "Max tokens per chunk.");
            var overlapTokensOption = new Option<int>("--overlap", () => 50, "Overlap tokens between chunks.");
            var preserveParagraphsOption = new Option<bool>("--preserve-paragraphs", () => true, "Preserve paragraphs.");

            cmd.AddArgument(fileArg);
            cmd.AddOption(maxTokensOption);
            cmd.AddOption(overlapTokensOption);
            cmd.AddOption(preserveParagraphsOption);

            cmd.SetHandler(async (string file, int maxTokens, int overlapTokens, bool preserveParagraphs) =>
            {
                var chunker = new TokenChunker(maxTokens, overlapTokens, preserveParagraphs);
                var content = await System.IO.File.ReadAllTextAsync(file);
                var document = new SourceDocument { Id = file, Content = content };
                var chunks = chunker.Chunk(document);

                foreach (var chunk in chunks)
                {
                    Console.WriteLine("--- Chunk ---");
                    Console.WriteLine(chunk.Content);
                    Console.WriteLine($"Token Count: {chunk.TokenCount}");
                    Console.WriteLine();
                }
            }, fileArg, maxTokensOption, overlapTokensOption, preserveParagraphsOption);

            return cmd;
        }
    }
}
