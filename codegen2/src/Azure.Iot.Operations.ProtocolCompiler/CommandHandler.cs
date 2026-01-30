// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    internal class CommandHandler
    {
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;

        public static readonly string[] SupportedLanguages = CommandPerformer.LanguageMap.Keys.ToArray();

        public static int GenerateCode(OptionContainer options)
        {
            ErrorLog errorLog = CommandPerformer.GenerateCode(options, (string msg, bool noNewline) =>
            {
                if (noNewline)
                {
                    Console.Write(msg);
                }
                else
                {
                    Console.WriteLine(msg);
                }
            });

            if (errorLog.HasErrors)
            {
                DisplayErrors(errorLog);
                DisplayWarnings(errorLog);
                return 1;
            }
            else
            {
                DisplayWarnings(errorLog);
                return 0;
            }
        }

        private static void DisplayErrors(ErrorLog errorLog)
        {
            if (errorLog.Errors.Count > 0 || errorLog.FatalError != null)
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine();
                Console.WriteLine($"{errorLog.Phase} FAILED with the following errors:");
                if (errorLog.FatalError != null)
                {
                    Console.WriteLine($"  FATAL: {FormatErrorRecord(errorLog.FatalError)}");
                }
                foreach (ErrorRecord error in errorLog.Errors.OrderBy(e => (e.Filename, e.LineNumber)))
                {
                    Console.WriteLine($"  ERROR: {FormatErrorRecord(error)}");
                }
                Console.ResetColor();
            }
        }

        private static void DisplayWarnings(ErrorLog errorLog)
        {
            if (errorLog.Warnings.Count > 0)
            {
                Console.ForegroundColor = WarningColor;
                Console.WriteLine();
                foreach (ErrorRecord error in errorLog.Warnings.OrderBy(e => (e.CrossRef, e.Filename, e.LineNumber)))
                {
                    Console.WriteLine($"  WARNING: {FormatErrorRecord(error)}");
                }
                Console.ResetColor();
            }
        }

        private static string FormatErrorRecord(ErrorRecord error)
        {
            string cfLineInfo = error.CfLineNumber > 0 ? $", cf. Line: {error.CfLineNumber}" : string.Empty;
            string lineInfo = error.LineNumber > 0 ? $", Line: {error.LineNumber}" : string.Empty;
            string fileInfo = error.Filename != string.Empty ? $" (File: {error.Filename}{lineInfo}{cfLineInfo})" : string.Empty;
            return $"{error.Message}{fileInfo}";
        }
    }
}
