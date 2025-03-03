namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;

    public static class Alert
    {
        public static void Warning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"WARNING -- {message}");
            Console.ResetColor();
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR -- {message}");
            Console.ResetColor();
        }

        public static void Fatal(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FATAL -- {message}");
            Environment.Exit(1);
        }
    }
}
