// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2Wot
{
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Opc2WotLib;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Microsoft.Extensions.FileSystemGlobbing;
    using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
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

            List<(DirectoryInfo Root, string Pattern)> rootedPatterns = options.NodeSetsSpec
                .Select(SplitGlobSpec)
                .ToList();

            Dictionary<string, (DirectoryInfo Root, Matcher Matcher)> matchersByRoot = new();
            foreach ((DirectoryInfo root, string pattern) in rootedPatterns)
            {
                string key = root.FullName;
                if (!matchersByRoot.TryGetValue(key, out var entry))
                {
                    entry = (root, new Matcher());
                    matchersByRoot[key] = entry;
                }

                entry.Matcher.AddInclude(pattern);
            }

            HashSet<string> seenInputPaths = new(StringComparer.OrdinalIgnoreCase);
            List<FileInfo> inputFiles = new();
            foreach ((DirectoryInfo root, Matcher matcher) in matchersByRoot.Values)
            {
                if (!root.Exists)
                {
                    continue;
                }

                PatternMatchingResult matchResult = matcher.Execute(new DirectoryInfoWrapper(root));
                foreach (FilePatternMatch match in matchResult.Files)
                {
                    string fullPath = Path.GetFullPath(Path.Combine(root.FullName, match.Path));
                    if (seenInputPaths.Add(fullPath))
                    {
                        inputFiles.Add(new FileInfo(fullPath));
                    }
                }
            }

            if (inputFiles.Count == 0)
            {
                AddUnlocatableError(ErrorCondition.ItemNotFound, $"No files match the given glob pattern(s): {string.Join(", ", options.NodeSetsSpec)}", errorLog);
                return errorLog;
            }

            OpcUaGraph opcUaGraph = new OpcUaGraph();

            foreach (FileInfo inputFile in inputFiles)
            {
                statusReceiver?.Invoke($"Processing file: {inputFile.FullName}", false);
                string modelText = File.ReadAllText(inputFile.FullName);
                opcUaGraph.AddNodeset(modelText);
            }

            if (!options.OutputDir.Exists)
            {
                options.OutputDir.Create();
            }

            LinkRelRuleEngine linkRelRuleEngine = new LinkRelRuleEngine();

            if (!options.Integrate)
            {
                statusReceiver?.Invoke("Skipping validation of Thing Model references in links because '--integrate' option is not set.", false);
            }

            foreach (string modelUri in opcUaGraph.GetModelUris())
            {
                errorLog.ClearRegistrations();

                WotThingCollection thingCollection = new WotThingCollection(opcUaGraph, opcUaGraph.GetOpcUaModelInfo(modelUri), linkRelRuleEngine, options.Integrate, options.InheritVars, options.IncludeTDs);

                string thingText = thingCollection.TransformText();

                string outFileName = $"{SpecMapper.GetSpecNameFromUri(modelUri)}.TM.json";
                string outFilePath = Path.Combine(options.OutputDir.FullName, outFileName);

                ValidateThing(thingText, errorLog, outFileName, validateReferences: options.Integrate);

                List<string> thingTypes = new();
                if (thingCollection.ThingDescriptions.Any())
                {
                    thingTypes.Add("Thing Descriptions");
                }
                if (thingCollection.ThingModels.Any())
                {
                    thingTypes.Add("Thing Models");
                }
                if (thingCollection.DataTypeModels.Any())
                {
                    thingTypes.Add("DataType Models");
                }

                if (thingCollection.ThingDescriptions.Any() || thingCollection.ThingModels.Any() || thingCollection.DataTypeModels.Any())
                {
                    statusReceiver?.Invoke($"Writing {string.Join(" and ", thingTypes)} for '{modelUri}' to '{outFileName}'", false);
                    File.WriteAllText(outFilePath, thingText);
                }
            }

            if (errorLog.HasErrors)
            {
                return errorLog;
            }

            errorLog.CheckForDuplicatesInThings();

            return errorLog;
        }

        private static void ValidateThing(string thingText, ErrorLog errorLog, string outFileName, bool validateReferences)
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

            Dictionary<string, TDThing> titleToThingMap = things.ToDictionary(t => t.Title!.Value.Value, t => t);

            foreach (TDThing thing in things)
            {
                HashSet<SerializationFormat> serializationFormats = new();
                if (thingValidator.TryValidateThing(new IntegralResolvingThing(thing, errorReporter, titleToThingMap), serializationFormats, validateReferences))
                {
                    errorReporter.RegisterNameOfThing(thing.Title!.Value.Value, thing.Title!.TokenIndex);
                }
            }

            thingValidator.ValidateThingCollection(things, null);
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

        private static (DirectoryInfo Root, string Pattern) SplitGlobSpec(string spec)
        {
            string normalized = spec.Replace('\\', '/');
            int firstWildcard = normalized.IndexOfAny(new[] { '*', '?', '[' });
            int splitIndex = firstWildcard < 0
                ? normalized.LastIndexOf('/')
                : normalized.LastIndexOf('/', firstWildcard);

            string rootPart;
            string patternPart;
            if (splitIndex < 0)
            {
                rootPart = ".";
                patternPart = normalized;
            }
            else
            {
                rootPart = normalized.Substring(0, splitIndex);
                patternPart = normalized.Substring(splitIndex + 1);
                if (rootPart.Length == 0)
                {
                    rootPart = "/";
                }
            }

            string fullRoot = Path.GetFullPath(rootPart);
            return (new DirectoryInfo(fullRoot), patternPart);
        }
    }
}
