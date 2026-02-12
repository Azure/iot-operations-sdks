// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Text;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Opc2WotLib;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal class Program
    {
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;

        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Opc2Wot <inputFolderPath> <outputFolderPath>");
                Console.WriteLine("Converts a collection of OPC UA Nodeset2 files to WoT Thing Models suitable for use with AIO.");
                Console.WriteLine("  <inputFolderPath>     Path to the input folder containing OPC UA Nodeset2 files.");
                Console.WriteLine("  <outputFolderPath>    Path to the output folder for the generated Thing Models.");
                return 1;
            }

            DirectoryInfo inputDirectory = new DirectoryInfo(args[0]);
            DirectoryInfo outputDirectory = new DirectoryInfo(args[1]);

            OpcUaGraph opcUaGraph = new OpcUaGraph();

            foreach (FileInfo inputFile in inputDirectory.GetFiles("*", SearchOption.AllDirectories))
            {
                if (inputFile.Name.EndsWith(".Nodeset2.xml", StringComparison.OrdinalIgnoreCase))
                {
/*
                    if (inputFile.Name.Equals("opc.ua.isa95-jobcontrol.nodeset2.xml", StringComparison.OrdinalIgnoreCase) ||
                        inputFile.Name.Equals("Opc.Ua.TMC.NodeSet2.xml", StringComparison.OrdinalIgnoreCase) ||
                        inputFile.Name.Equals("Opc.Ua.Woodworking.NodeSet2.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"SKIPPING FILE: {inputFile.FullName}");
                        continue;
                    }
*/
                    Console.WriteLine($"Processing file: {inputFile.FullName}");
                    string modelText = inputFile.OpenText().ReadToEnd();
                    opcUaGraph.AddNodeset(modelText);
                }
            }

            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }

            ErrorLog errorLog = new(string.Empty);

            LinkRelRuleEngine linkRelRuleEngine = new LinkRelRuleEngine();

            foreach (string modelUri in opcUaGraph.GetModelUris())
            {
                WotThingCollection thingCollection = new WotThingCollection(opcUaGraph.GetOpcUaModelInfo(modelUri), linkRelRuleEngine);

                string thingText = thingCollection.TransformText();

                string outFileName = $"{SpecMapper.GetSpecNameFromUri(modelUri)}.TM.json";
                string outFilePath = Path.Combine(outputDirectory.FullName, outFileName);

                ValidateThing(thingText, errorLog, outFileName);

                Console.WriteLine($"Writing Thing Model for '{modelUri}' to '{outFileName}'");
                File.WriteAllText(outFilePath, thingText);
            }

            if (errorLog.HasErrors)
            {
                DisplayErrors(errorLog);
                return 1;
            }

            errorLog.CheckForDuplicatesInThings();
            if (errorLog.HasErrors)
            {
                DisplayErrors(errorLog);
                return 1;
            }

            return 0;
        }

        private static void ValidateThing(string thingText, ErrorLog errorLog, string outFileName)
        {
            byte[] thingBytes = Encoding.UTF8.GetBytes(thingText);
            ErrorReporter errorReporter = new ErrorReporter(errorLog, outFileName, thingBytes);
            ThingValidator thingValidator = new ThingValidator(errorReporter);

            List<TDThing> things;
            try
            {
                things = TDParser.Parse(thingBytes);
            }
            catch (Exception ex)
            {
                errorReporter.ReportJsonException(ex);
                return;
            }

            HashSet<SerializationFormat> serializationFormats = new();
            foreach (TDThing thing in things)
            {
                if (thingValidator.TryValidateThing(thing, serializationFormats))
                {
                    errorReporter.RegisterNameOfThing(thing.Title!.Value.Value, thing.Title!.TokenIndex);
                }
            }

            thingValidator.ValidateThingCollection(things);
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
                foreach (ErrorRecord error in errorLog.Errors.OrderBy(e => (e.CrossRef, e.Filename, e.LineNumber)))
                {
                    Console.WriteLine($"  ERROR: {FormatErrorRecord(error)}");
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
