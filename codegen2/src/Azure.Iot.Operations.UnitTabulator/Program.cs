// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.UnitTabulator
{
    using System;
    using System.CommandLine;
    using System.IO;

    internal class Program
    {
        static int Main(string[] args)
        {
            var outDirOption = new Option<DirectoryInfo>("--outDir")
            {
                Description = "Directory for receiving generated code",
                HelpName = "DIRPATH",
                Required = true,
            };

            var langOption = new Option<string>("--lang")
            {
                Description = $"Programming language for generated code",
                HelpName = string.Join('|', CommandHandler.SupportedLanguages),
                Required = true,
            };

            var tableKindOption = new Option<TableKind>("--kind")
            {
                Description = "Kind of tables to generate",
                HelpName = string.Join('|', Enum.GetNames<TableKind>()),
                Required = true,
            };

            var rootCommand = new RootCommand("Tool for generating programming-language-specific unit tables from the QUDT unit ontology");
            rootCommand.Add(outDirOption);
            rootCommand.Add(langOption);
            rootCommand.Add(tableKindOption);

            rootCommand.SetAction(parseResult =>
            {
                Environment.ExitCode = CommandHandler.PopulateTables(new OptionContainer
                {
                    OutputDir = parseResult.GetValue(outDirOption)!,
                    Language = parseResult.GetValue(langOption)!,
                    TableKind = parseResult.GetValue(tableKindOption)!,
                });
            });

            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }
    }
}
