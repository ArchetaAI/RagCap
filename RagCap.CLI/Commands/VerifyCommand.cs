using System.CommandLine;
using RagCap.Core.Validation;

namespace RagCap.CLI.Commands
{
    public static class VerifyCommand
    {
        public static Command Create()
        {
            var cmd = new Command("verify", "Validate a .ragcap capsule file");
            var fileArg = new Argument<string>("file", "Path to the capsule (.ragcap)");

            cmd.AddArgument(fileArg);

            cmd.SetHandler((string file) =>
            {
                var validator = new CapsuleValidator();
                var result = validator.Validate(file);

                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✔ {result.Message}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✘ {result.Message}");
                }
                Console.ResetColor();
            }, fileArg);

            return cmd;
        }
    }
}
