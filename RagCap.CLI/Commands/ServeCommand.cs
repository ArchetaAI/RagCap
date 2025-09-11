
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace RagCap.CLI.Commands
{
    public class ServeCommand : AsyncCommand<ServeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<CAPSULE_PATH>")]
            public string CapsulePath { get; set; }

            [CommandOption("--port")]
            [DefaultValue(5000)]
            public int Port { get; set; }

            [CommandOption("--host")]
            [DefaultValue("localhost")]
            public string Host { get; set; }

            [CommandOption("--log-level")]
            [DefaultValue(LogLevel.Info)]
            public LogLevel LogLevel { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            AnsiConsole.MarkupLine($"[green]🚀 RagCap server running at http://{settings.Host}:{settings.Port}[/]");
            await CreateHostBuilder(settings).Build().RunAsync();
            return 0;
        }

        public static IHostBuilder CreateHostBuilder(Settings settings) =>
            Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(settings.LogLevel);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup(provider => new RagCap.Server.Startup(settings.CapsulePath));
                    webBuilder.UseUrls($"http://{settings.Host}:{settings.Port}");
                });
    }
}
