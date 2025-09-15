using RagCap.Core.Capsule;
using RagCap.Core.Chunking;
using RagCap.Core.Ingestion;
using RagCap.Core.Pipeline;
using RagCap.Core.Processing;
using RagCap.Core.Recipes;
using RagCap.Core.Utils;
using Spectre.Console;
using Spectre.Console.Cli;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System;

namespace RagCap.CLI.Commands
{
    public class DebugChunkCommand : AsyncCommand<DebugChunkCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--input")]
            public string? Input { get; set; }

            [CommandOption("--recipe")]
            public string? RecipePath { get; set; }

            [CommandOption("--preview-chars")]
            public int PreviewChars { get; set; } = 120;

            [CommandOption("--show-chunks")]
            public bool ShowChunks { get; set; } = false;

            [CommandOption("--dump-dir")]
            public string? DumpDir { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            Recipe? recipe = null;
            if (!string.IsNullOrEmpty(settings.RecipePath))
            {
                if (!File.Exists(settings.RecipePath))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Recipe file not found at '{settings.RecipePath}'[/]");
                    return 1;
                }

                var recipeContent = await File.ReadAllTextAsync(settings.RecipePath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                try
                {
                    recipe = deserializer.Deserialize<Recipe>(recipeContent);
                }
                catch (System.Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing recipe file:[/]");
                    AnsiConsole.WriteException(ex);
                    return 1;
                }
            }

            var inputPath = settings.Input ?? recipe?.Sources?.FirstOrDefault()?.Path;
            if (string.IsNullOrEmpty(inputPath))
            {
                AnsiConsole.MarkupLine("[red]Error: Input path must be specified via --input or recipe file.[/]");
                return 1;
            }

            // Configure chunker and preprocessor like BuildPipeline
            var chunkSize = recipe?.Chunking?.Size ?? 500;
            var overlap = recipe?.Chunking?.Overlap ?? 50;
            var tokenChunker = new TokenChunker(chunkSize, overlap);

            var boilerplate = recipe?.Preprocess?.Boilerplate ?? true;
            var preserveCode = recipe?.Preprocess?.PreserveCode ?? true;
            var flattenTables = recipe?.Preprocess?.FlattenTables ?? true;
            var detectLanguage = recipe?.Preprocess?.DetectLanguage ?? true;
            var preprocessor = new Preprocessor(boilerplate, preserveCode, flattenTables, detectLanguage);

            // Resolve files like BuildPipeline
            var files = new List<string>();
            var sourcesFromRecipe = recipe?.Sources?.Select(s => s.Path).ToList();
            if (sourcesFromRecipe != null && sourcesFromRecipe.Count > 0)
            {
                foreach (var source in sourcesFromRecipe)
                {
                    if (Directory.Exists(source)) files.AddRange(Directory.GetFiles(source, "*", SearchOption.AllDirectories));
                    else if (File.Exists(source)) files.Add(source);
                }
            }
            else
            {
                if (Directory.Exists(inputPath)) files.AddRange(Directory.GetFiles(inputPath, "*", SearchOption.AllDirectories));
                else if (File.Exists(inputPath)) files.Add(inputPath);
            }

            var tokenizer = new Tokenizer();
            int totalChunks = 0;
            int sources = 0;

            foreach (var file in files)
            {
                try
                {
                    var loader = FileLoaderFactory.GetLoader(file);
                    var content = loader.LoadContent(file);
                    var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                    var sourceDoc = new SourceDocument
                    {
                        Id = "temp",
                        Path = file,
                        Hash = HashUtils.GetSha256Hash(content),
                        Content = content,
                        DocumentType = ext
                    };

                    var processed = preprocessor.Process(sourceDoc);
                    sourceDoc.Content = processed;

                    var chunks = tokenChunker.Chunk(sourceDoc);
                    sources++;
                    totalChunks += chunks.Count;

                    // Report per-file
                    AnsiConsole.WriteLine($"File: {file}");
                    AnsiConsole.WriteLine($"  Type: {ext}");
                    AnsiConsole.WriteLine($"  Processed length: {processed.Length} chars");
                    AnsiConsole.WriteLine($"  Tokens total: {tokenizer.CountTokens(processed)}");
                    AnsiConsole.WriteLine($"  Chunks: {chunks.Count}");
                    if (settings.ShowChunks)
                    {
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var c = chunks[i];
                            var preview = (c.Content ?? string.Empty);
                            if (preview.Length > settings.PreviewChars) preview = preview.Substring(0, settings.PreviewChars) + "…";
                            AnsiConsole.WriteLine($"    [{i + 1}] tokens={c.TokenCount} preview=\"{preview.Replace("\n", "\\n")}\"");
                        }
                    }

                    // Optional dump to disk for full inspection
                    if (!string.IsNullOrWhiteSpace(settings.DumpDir))
                    {
                        var baseDir = settings.DumpDir!;
                        Directory.CreateDirectory(baseDir);
                        var safeName = Path.GetFileName(file);
                        var outDir = Path.Combine(baseDir, safeName);
                        Directory.CreateDirectory(outDir);

                        // Write processed text
                        await File.WriteAllTextAsync(Path.Combine(outDir, "processed.txt"), processed);

                        // Write chunks
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var c = chunks[i];
                            var name = $"chunk_{i + 1:000}_tokens_{c.TokenCount}.txt";
                            await File.WriteAllTextAsync(Path.Combine(outDir, name), c.Content ?? string.Empty);
                        }

                        AnsiConsole.MarkupLine($"[green]Dumped:[/] {outDir}");
                    }
                    AnsiConsole.WriteLine("");
                }
                catch (NotSupportedException ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Skipping unsupported file type:[/] {file} ({ex.Message})");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error processing file:[/] {file}");
                    AnsiConsole.WriteException(ex);
                }
            }

            AnsiConsole.MarkupLine($"[green]Summary:[/] sources={sources}, chunks={totalChunks}");
            return 0;
        }
    }
}
