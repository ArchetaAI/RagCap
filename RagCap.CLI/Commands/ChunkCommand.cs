using RagCap.Core.Chunking;
using RagCap.Core.Capsule;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class ChunkCommand : AsyncCommand<ChunkCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<file>")]
            public string File { get; set; }

            [CommandOption("--tokens")]
            [DefaultValue(512)]
            public int MaxTokens { get; set; }

            [CommandOption("--overlap")]
            [DefaultValue(50)]
            public int OverlapTokens { get; set; }

            [CommandOption("--preserve-paragraphs")]
            [DefaultValue(true)]
            public bool PreserveParagraphs { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var chunker = new TokenChunker(settings.MaxTokens, settings.OverlapTokens, settings.PreserveParagraphs);
            var content = await System.IO.File.ReadAllTextAsync(settings.File);
            var document = new SourceDocument { Id = settings.File, Content = content };
            var chunks = chunker.Chunk(document);

            foreach (var chunk in chunks)
            {
                AnsiConsole.WriteLine("--- Chunk ---");
                AnsiConsole.WriteLine(chunk.Content);
                AnsiConsole.WriteLine($"Token Count: {chunk.TokenCount}");
                AnsiConsole.WriteLine();
            }
            return 0;
        }
    }
}