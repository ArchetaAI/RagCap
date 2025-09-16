using System;

namespace RagCap.Core.Utils
{
    public static class Logging
    {
        public static bool Verbose { get; set; } = false;

        public static void Info(string message)
        {
            Console.WriteLine(message);
        }

        public static void Debug(string message)
        {
            if (Verbose)
            {
                Console.WriteLine(message);
            }
        }

        public static void Error(string message)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }
    }
}
