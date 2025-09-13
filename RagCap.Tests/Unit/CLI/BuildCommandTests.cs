using Moq;
using RagCap.CLI.Commands;
using RagCap.Core.Pipeline;
using Spectre.Console.Cli;
using System.Threading.Tasks;
using Xunit;

namespace RagCap.Tests.Unit.CLI
{
    public class BuildCommandTests
    {
        [Fact]
        public async Task BuildCommand_ShouldCallBuildPipeline()
        {
            var buildPipelineMock = new Mock<IBuildPipeline>();
            var command = new BuildCommand(buildPipelineMock.Object);

            var settings = new BuildCommand.Settings
            {
                Input = "test-input",
                Output = "test-output.ragcap"
            };

            var context = new CommandContext(null, "build", null);
            await command.ExecuteAsync(context, settings);

            buildPipelineMock.Verify(p => p.RunAsync(settings.Input, null), Times.Once);
        }
    }

    // Dummy interface for mocking
    public interface IBuildPipeline
    {
        Task RunAsync(string inputPath, System.Collections.Generic.List<string> sourcesFromRecipe);
    }
}
