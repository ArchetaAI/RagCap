using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagCap.CLI.Commands;
using Spectre.Console.Cli;
using Xunit;

namespace RagCap.Tests.Integration;

public class ExportTests
{
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    [Fact]
    public async Task ExportParquet_ShouldCreateParquetFile()
    {
        var capsulePath = "test_parquet.ragcap";
        var outputPath = "test.parquet";

        await _semaphore.WaitAsync();
        try
        {
            TestCapsule.Create(capsulePath);

            var command = new ExportCommand();
            var settings = new ExportCommand.Settings
            {
                CapsulePath = capsulePath,
                Format = "parquet",
                OutputPath = outputPath
            };

            var context = new CommandContext(new List<string>(), new MockRemainingArguments(), "export", new object());
            await command.ExecuteAsync(context, settings);

            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            File.Delete(capsulePath);
            if(File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
            _semaphore.Release();
        }
    }

    [Fact]
    public async Task ExportFaiss_ShouldCreateFaissFile()
    {
        var capsulePath = "test_faiss.ragcap";
        var outputPath = "test.faiss";

        await _semaphore.WaitAsync();
        try
        {
            TestCapsule.Create(capsulePath);

            var command = new ExportCommand();
            var settings = new ExportCommand.Settings
            {
                CapsulePath = capsulePath,
                Format = "faiss",
                OutputPath = outputPath
            };

            var context = new CommandContext(new List<string>(), new MockRemainingArguments(), "export", new object());
            await command.ExecuteAsync(context, settings);

            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            File.Delete(capsulePath);
            if(File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
            _semaphore.Release();
        }
    }
}

public class MockRemainingArguments : IRemainingArguments
{
    public IReadOnlyList<string> Raw => new List<string>();
    public ILookup<string, string?> Parsed => new List<string>().ToLookup(x => x);
}