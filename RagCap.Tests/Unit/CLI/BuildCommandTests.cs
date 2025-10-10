using RagCap.CLI.Commands;
using System.Threading.Tasks;
using Xunit;

namespace RagCap.Tests.Unit.CLI
{
    public class BuildCommandTests
    {
        [Fact]
        public async Task BuildCommand_MissingInput_ReturnsError()
        {
            var cmd = new BuildCommand();
            var settings = new BuildCommand.Settings
            {
                Input = null,
                Output = "out.ragcap"
            };
            var exit = await cmd.ExecuteAsync(context: null!, settings);
            Assert.Equal(1, exit);
        }

        [Fact]
        public async Task BuildCommand_MissingOutput_ReturnsError()
        {
            var cmd = new BuildCommand();
            var settings = new BuildCommand.Settings
            {
                Input = "in",
                Output = null
            };
            var exit = await cmd.ExecuteAsync(context: null!, settings);
            Assert.Equal(1, exit);
        }
    }
}
