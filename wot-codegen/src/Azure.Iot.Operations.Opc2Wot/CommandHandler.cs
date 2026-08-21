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

            inputFiles.Sort((left, right) =>
            {
                int comparison = StringComparer.OrdinalIgnoreCase.Compare(left.FullName, right.FullName);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.FullName, right.FullName);
            });

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

            Dictionary<string, WotThingDocument> documentsByFileName = new(StringComparer.Ordinal);
            foreach (string modelUri in opcUaGraph.GetModelUris())
            {
                WotThingCollection thingCollection = new WotThingCollection(opcUaGraph, opcUaGraph.GetOpcUaModelInfo(modelUri), linkRelRuleEngine, options.Integrate, options.InheritVars, options.IncludeTDs);
                foreach (WotThingDocument document in thingCollection.GetDocuments())
                {
                    if (documentsByFileName.TryGetValue(document.FileName, out WotThingDocument? existingDocument))
                    {
                        if (existingDocument.Text != document.Text)
                        {
                            AddUnlocatableError(ErrorCondition.Duplication, $"Multiple Thing Models would be written to '{document.FileName}' with different content.", errorLog);
                        }
                    }
                    else
                    {
                        documentsByFileName.Add(document.FileName, document);
                    }
                }
            }

            if (errorLog.HasErrors)
            {
                return errorLog;
            }

            errorLog.ClearRegistrations();
            ValidateThings(documentsByFileName.Values, errorLog, validateReferences: options.Integrate);
            errorLog.CheckForDuplicatesInThings();

            if (errorLog.HasErrors)
            {
                return errorLog;
            }

            foreach (WotThingDocument document in documentsByFileName.Values.OrderBy(d => d.FileName, StringComparer.Ordinal))
            {
                statusReceiver?.Invoke($"Writing Thing document to '{document.FileName}'", false);
                File.WriteAllText(Path.Combine(options.OutputDir.FullName, document.FileName), document.Text);
            }

            return errorLog;
        }

        private static void ValidateThings(IEnumerable<WotThingDocument> documents, ErrorLog errorLog, bool validateReferences)
        {
            List<(TDThing Thing, ErrorReporter ErrorReporter)> parsedThings = new();

            foreach (WotThingDocument document in documents)
            {
                byte[] thingBytes = Encoding.UTF8.GetBytes(document.Text);
                ErrorReporter errorReporter = new ErrorReporter(errorLog, document.FileName, thingBytes);

                try
                {
                    parsedThings.AddRange(TDParser.Parse(thingBytes).Select(thing => (thing, errorReporter)));
                }
                catch (Exception ex)
                {
                    errorReporter.ReportJsonException(ex);
                }
            }

            Dictionary<string, TDThing> hrefToThingMap = new(StringComparer.Ordinal);
            foreach ((TDThing thing, _) in parsedThings)
            {
                if (thing.Title != null)
                {
                    hrefToThingMap.TryAdd($"#{TDValues.HrefTitlePrefix}{thing.Title.Value.Value}", thing);
                }

                if (thing.Id != null)
                {
                    hrefToThingMap.TryAdd(thing.Id.Value.Value, thing);
                }
            }

            foreach ((TDThing thing, ErrorReporter errorReporter) in parsedThings)
            {
                ThingValidator thingValidator = new ThingValidator(errorReporter, requireThingModelForms: false);
                HashSet<SerializationFormat> serializationFormats = new();
                if (thingValidator.TryValidateThing(new IntegralResolvingThing(thing, errorReporter, hrefToThingMap), serializationFormats, validateReferences))
                {
                    errorReporter.RegisterNameOfThing(thing.Title!.Value.Value, thing.Title!.TokenIndex);
                }

                thingValidator.ValidateThingCollection(new List<TDThing> { thing }, null);
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
