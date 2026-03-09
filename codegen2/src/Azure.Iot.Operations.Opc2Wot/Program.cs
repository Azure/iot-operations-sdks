// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using System;
    using System.CommandLine;
    using System.IO;

    internal class Program
    {
        static void Main(string[] args)
        {
            var nodeSetsOption = new Option<DirectoryInfo>("--nodeSets")
            {
                Description = "Path to a folder containing OPC UA Nodeset2 files to process",
                HelpName = "DIRPATH ...",
                Required = true,
            };

            var outDirOption = new Option<DirectoryInfo>("--outDir")
            {
                Description = "Path to a folder for placing files that will each contain a collection of WoT Thing Models",
                HelpName = "DIRPATH",
                Required = true,
            };

            var integrateOption = new Option<bool>("--integrate")
            {
                Description = "Integrate all referenced Thing Models into each Thing Model collection, making each output file self-contained",
            };

            var inheritVarsOption = new Option<bool>("--inheritVars")
            {
                Description = "Add a 'dtv:includeInherited' property to root-level forms where appropriate, so that inherited variables are included in aggregate events and properties",
            };

            var rootCommand = new RootCommand("Tool for converting OPC UA specs to WoT Thing Models for use in Akri");
            rootCommand.Add(nodeSetsOption);
            rootCommand.Add(outDirOption);
            rootCommand.Add(integrateOption);
            rootCommand.Add(inheritVarsOption);

            rootCommand.SetAction(parseResult =>
            {
                Environment.ExitCode = CommandHandler.ConvertSpecs(new OptionContainer
                {
                    NodeSetsDir = parseResult.GetValue(nodeSetsOption)!,
                    OutputDir = parseResult.GetValue(outDirOption)!,
                    Integrate = parseResult.GetValue(integrateOption),
                    InheritVars = parseResult.GetValue(inheritVarsOption),
                });
            });

            ParseResult parseResult = rootCommand.Parse(args);
            parseResult.Invoke();
        }
    }
}
