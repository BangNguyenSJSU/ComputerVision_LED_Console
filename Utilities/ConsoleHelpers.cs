using System;
using System.Text;

namespace ComputerVision_LED_Console.Utilities
{
    public static class ConsoleHelpers
    {
        // Reads a line from the console while echoing '*' for each keystroke.
        // Backspace edits the buffer; Enter completes; Escape cancels (returns null).
        // Falls back to Console.ReadLine when stdin is redirected (tests, piped input)
        // because ReadKey requires an interactive console.
        public static string? ReadMaskedLine()
        {
            if (Console.IsInputRedirected) return Console.ReadLine();

            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return sb.ToString();
                }
                if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    return null;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (key.KeyChar == '\0') continue;
                sb.Append(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
