// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Opc2WotLib;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Text;

    internal class CommandHandler
    {
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;

        public static int ConvertSpecs(OptionContainer options)
        {
            ErrorLog errorLog = ConvertSpecs(options, (string msg, bool noNewline) =>
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

        public static ErrorLog ConvertSpecs(OptionContainer options, Action<string, bool> statusReceiver)
        {
            ErrorLog errorLog = new(string.Empty);

            if (!options.NodeSetsDir.Exists)
            {
                AddUnlocatableError(ErrorCondition.ItemNotFound, $"Specified NodeSets directory '{options.NodeSetsDir.FullName}' does not exist.", errorLog);
                return errorLog;
            }

            OpcUaGraph opcUaGraph = new OpcUaGraph();

            foreach (FileInfo inputFile in options.NodeSetsDir.GetFiles("*", SearchOption.AllDirectories))
            {
                if (inputFile.Name.EndsWith(".Nodeset2.xml", StringComparison.OrdinalIgnoreCase))
                {
                    statusReceiver?.Invoke($"Processing file: {inputFile.FullName}", false);
                    string modelText = inputFile.OpenText().ReadToEnd();
                    opcUaGraph.AddNodeset(modelText);
                }
            }

            if (!options.OutputDir.Exists)
            {
                options.OutputDir.Create();
            }

            LinkRelRuleEngine linkRelRuleEngine = new LinkRelRuleEngine();

            foreach (string modelUri in opcUaGraph.GetModelUris())
            {
                WotThingCollection thingCollection = new WotThingCollection(opcUaGraph.GetOpcUaModelInfo(modelUri), linkRelRuleEngine);

                string thingText = thingCollection.TransformText();

                string outFileName = $"{SpecMapper.GetSpecNameFromUri(modelUri)}.TM.json";
                string outFilePath = Path.Combine(options.OutputDir.FullName, outFileName);

                ValidateThing(thingText, errorLog, outFileName);

                statusReceiver?.Invoke($"Writing Thing Model for '{modelUri}' to '{outFileName}'", false);
                File.WriteAllText(outFilePath, thingText);
            }

            if (errorLog.HasErrors)
            {
                return errorLog;
            }

            errorLog.CheckForDuplicatesInThings();

            return errorLog;
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

        private static void AddUnlocatableError(ErrorCondition condition, string message, ErrorLog errorLog)
        {
            errorLog.AddError(ErrorLevel.Error, condition, message, string.Empty, 0);
        }
    }
}
