namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.EnvoyGenerator;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Azure.Iot.Operations.TypeGenerator;
    using Azure.Iot.Operations.SchemaGenerator;

    internal class CommandHandler
    {
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;

        private static readonly Dictionary<string, TargetLanguage> LanguageMap = new()
        {
            { "csharp", TargetLanguage.CSharp },
            { "rust", TargetLanguage.Rust },
            { "none", TargetLanguage.None },
        };

        public static readonly string[] SupportedLanguages = LanguageMap.Keys.ToArray();

        public static int GenerateCode(OptionContainer options)
        {
            try
            {
                WarnOnSuspiciousOptions(options);

                if (!TryConfirmValidOptions(options))
                {
                    return 1;
                }

                string projectName = LegalizeProjectName(options.OutputDir.Name);
                TargetLanguage targetLanguage = LanguageMap[options.Language.ToLowerInvariant()];

                ErrorLog errorLog = new(options.WorkingDir.FullName);

                List<ParsedThing> parsedThings = ParseThings(options.ThingFiles, errorLog);

                if (errorLog.HasErrors)
                {
                    DisplayErrors("Parsing", errorLog);
                    return 1;
                }

                Dictionary<SerializationFormat, List<GeneratedItem>> generatedSchemas = SchemaGenerator.GenerateSchemas(parsedThings, projectName, options.WorkingDir);

                errorLog.CheckForDuplicatesInThings();
                if (errorLog.HasErrors)
                {
                    DisplayErrors("Schema generation", errorLog);
                    return 1;
                }

                foreach (List<GeneratedItem> schemas in generatedSchemas.Values)
                {
                    WriteItems(schemas, options.WorkingDir);
                }

                if (targetLanguage == TargetLanguage.None)
                {
                    Console.WriteLine("No code generation requested; exiting after schema generation.");
                    return 0;
                }

                FileInfo[] schemaFiles = options.SchemaFiles.SelectMany(fs => Directory.GetFiles(Path.GetDirectoryName(fs) ?? string.Empty, Path.GetFileName(fs)), (_, f) => new FileInfo(f)).ToArray();
                if (schemaFiles.Length > 0)
                {
                    ImportSchemas(schemaFiles, generatedSchemas);
                }

                string? typeNameInfoText = options.TypeNamerFile?.OpenText()?.ReadToEnd();
                TypeNamer typeNamer = new TypeNamer(typeNameInfoText);

                List<GeneratedItem> generatedTypes = new();
                foreach (KeyValuePair<SerializationFormat, List<GeneratedItem>> schemaSet in generatedSchemas)
                {
                    Dictionary<string, string> schemaTextsByName = schemaSet.Value.ToDictionary(s => Path.GetFullPath(Path.Combine(options.WorkingDir.FullName, s.FolderPath, s.FileName)).Replace('\\', '/'), s => s.Content);
                    TypeGenerator typeGenerator = new TypeGenerator(schemaSet.Key, targetLanguage, typeNamer, errorLog);
                    generatedTypes.AddRange(typeGenerator.GenerateTypes(schemaTextsByName, new CodeName(options.GenNamespace), projectName, options.OutputSourceSubdir));
                }

                errorLog.CheckForDuplicatesInSchemas();
                if (errorLog.HasErrors)
                {
                    DisplayErrors("Type generation", errorLog);
                    return 1;
                }

                WriteItems(generatedTypes, options.OutputDir);

                List<string> typeNames = generatedTypes.Select(gt => Path.GetFileNameWithoutExtension(gt.FileName)).ToList();

                string? sdkPath = options.SdkPath != null ? Path.GetRelativePath(options.OutputDir.FullName, options.SdkPath) : null;

                List<GeneratedItem> generatedEnvoys = EnvoyGenerator.GenerateEnvoys(
                    parsedThings,
                    generatedSchemas.Keys.ToList(),
                    targetLanguage,
                    options.GenNamespace,
                    projectName,
                    sdkPath,
                    typeNames,
                    options.OutputSourceSubdir,
                    generateClient: !options.ServerOnly,
                    generateServer: !options.ClientOnly,
                    generateProject: !options.NoProj,
                    defaultImpl: options.DefaultImpl);
                WriteItems(generatedEnvoys, options.OutputDir);

                DisplayWarnings(errorLog);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            return 0;
        }

        private static List<ParsedThing> ParseThings(FileInfo[] thingFiles, ErrorLog errorLog)
        {
            List<ParsedThing> parsedThings = new();

            foreach (FileInfo thingFile in thingFiles)
            {
                Console.Write($"Parsing thing description file: {thingFile.Name} ...");

                using (StreamReader thingReader = thingFile.OpenText())
                {
                    string thingText = thingReader.ReadToEnd();
                    byte[] thingBytes = Encoding.UTF8.GetBytes(thingText);
                    ErrorReporter errorReporter = new ErrorReporter(errorLog, thingFile.FullName, thingBytes);

                    if (TryGetThings(errorReporter, thingBytes, out List<TDThing>? things))
                    {
                        int thingCount = 0;
                        foreach (TDThing thing in things)
                        {
                            ValueTracker<StringHolder>? schemaNamesFilename = thing.Links?.Elements?.FirstOrDefault(l => l.Value.Rel?.Value.Value == TDValues.RelationSchemaNaming)?.Value.Href;
                            if (TryGetSchemaNamer(errorReporter, thingFile.DirectoryName!, schemaNamesFilename, out SchemaNamer? schemaNamer))
                            {
                                thingCount++;
                                parsedThings.Add(new ParsedThing(thing, thingFile.Name, thingFile.DirectoryName!, schemaNamer, errorReporter));
                            }
                        }

                        Console.WriteLine($" {thingCount} {(thingCount == 1 ? "TD" : "TDs")} parsed");
                    }
                }
            }

            return parsedThings;
        }

        private static bool TryGetSchemaNamer(ErrorReporter errorReporter, string folderPath, ValueTracker<StringHolder>? namerFilename, [NotNullWhen(true)] out SchemaNamer? schemaNamer)
        {
            if (namerFilename == null)
            {
                schemaNamer = new SchemaNamer();
                return true;
            }

            FileInfo namerFile = new FileInfo(Path.Combine(folderPath, namerFilename.Value.Value));
            if (!namerFile.Exists)
            {
                errorReporter.ReportError($"Could not find schema naming file '{namerFilename.Value.Value}'.", namerFilename.TokenIndex);

                schemaNamer = null;
                return false;
            }

            string schemaNameInfoText = namerFile.OpenText().ReadToEnd();

            try
            {
                schemaNamer = new SchemaNamer(schemaNameInfoText);
                return true;
            }
            catch (Exception ex)
            {
                errorReporter.ReportError($"Failed to parse schema naming file '{namerFilename.Value.Value}': {ex.Message}", namerFilename.TokenIndex);
                schemaNamer = null;
                return false;
            }
        }

        private static bool TryGetThings(ErrorReporter errorReporter, byte[] thingBytes, [NotNullWhen(true)] out List<TDThing>? things)
        {
            try
            {
                things = TDParser.Parse(thingBytes);
            }
            catch (Exception ex)
            {
                errorReporter.ReportJsonException(ex);
                things = null;
                return false;
            }

            foreach (TDThing thing in things)
            {
                foreach (ITraversable item in thing.Traverse())
                {
                    if (item is ISourceTracker tracker && tracker.DeserializingFailed)
                    {
                        errorReporter.ReportError($"TD deserialization error: {tracker.DeserializationError ?? string.Empty}.", tracker.TokenIndex);
                    }

                    if (item is ValueTracker<TDDataSchema> dataSchema && dataSchema.Value.Ref != null)
                    {
                        errorReporter.RegisterReferenceFromThing(dataSchema.Value.Ref.TokenIndex, dataSchema.Value.Ref.Value.Value);
                    }
                }
            }

            return true;
        }

        private static void WriteItems(List<GeneratedItem> generatedItems, DirectoryInfo destDir)
        {
            foreach (GeneratedItem genItem in generatedItems)
            {
                DirectoryInfo folderPath = new DirectoryInfo(Path.Combine(destDir.FullName, genItem.FolderPath));
                if (!folderPath.Exists)
                {
                    folderPath.Create();
                }

                string filePath = Path.Combine(folderPath.FullName, genItem.FileName);
                File.WriteAllText(filePath, genItem.Content);
                Console.WriteLine($"  Generated {filePath}");
            }
        }

        private static void ImportSchemas(FileInfo[] extSchemaFiles, Dictionary<SerializationFormat, List<GeneratedItem>> generatedSchemas)
        {
            foreach (FileInfo schemaFile in extSchemaFiles)
            {
                SerializationFormat format = schemaFile.Name switch
                {
                    string n when n.EndsWith(".json", StringComparison.OrdinalIgnoreCase) => SerializationFormat.Json,
                    _ => SerializationFormat.None,
                };

                if (!generatedSchemas.TryGetValue(format, out List<GeneratedItem>? schemas))
                {
                    schemas = new();
                    generatedSchemas[format] = schemas;
                }

                schemas.Add(new GeneratedItem(schemaFile.OpenText().ReadToEnd(), schemaFile.Name, schemaFile.Directory!.FullName));
            }
        }

        private static string LegalizeProjectName(string fsName)
        {
            return string.Join('.', fsName.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(s => (char.IsNumber(s[0]) ? "_" : "") + Regex.Replace(s, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant)));
        }

        private static bool TryConfirmValidOptions(OptionContainer options)
        {
            bool anyThingFiles = options.ThingFiles.Length > 0;
            bool anySchemaFiles = options.SchemaFiles.Any(fs => Directory.GetFiles(Path.GetDirectoryName(fs) ?? string.Empty, Path.GetFileName(fs)).Any());

            if (!anyThingFiles && !anySchemaFiles)
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine($"No Thing Description files specified, and no schema files {(options.SchemaFiles.Length > 0 ? "found" : "specified")}.");
                Console.WriteLine("Use option --help for CLI usage and options.");
                Console.ResetColor();
                return false;
            }

            if (!SupportedLanguages.Contains(options.Language))
            {
                string langCondition = string.IsNullOrEmpty(options.Language) ? "language not specified" : $"language '{options.Language}' not recognized";
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine($"{langCondition}; language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))} (use 'none' for no code generation)");
                Console.ResetColor();
                return false;
            }

            if (options.ClientOnly && options.ServerOnly)
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine("options --clientOnly and --serverOnly are mutually exclusive");
                Console.ResetColor();
                return false;
            }

            if (options.ThingFiles.Any(mf => !mf.Exists))
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine("All Thing Description files must exist.  Non-existent files specified:");
                foreach (FileInfo f in options.ThingFiles.Where(tf => !tf.Exists))
                {
                    Console.WriteLine($"  {f.FullName}");
                }
                Console.ResetColor();
                return false;
            }

            return true;
        }

        private static void WarnOnSuspiciousOptions(OptionContainer options)
        {
            if (!options.WorkingDir.Exists)
            {
                WarnOnSuspiciousOption("workingDir", options.WorkingDir.Name);
            }

            if (!options.OutputDir.Exists)
            {
                WarnOnSuspiciousOption("outDir", options.OutputDir.Name);
            }

            WarnOnSuspiciousOption("namespace", options.GenNamespace);
            WarnOnSuspiciousOption("sdkPath", options.SdkPath);
        }

        private static void WarnOnSuspiciousOption(string optionName, string? pathName)
        {
            if (pathName != null && pathName.StartsWith("--"))
            {
                Console.ForegroundColor = WarningColor;
                Console.WriteLine($"Warning: {optionName} \"{pathName}\" looks like a flag.  Did you forget to specify a value?");
                Console.ResetColor();
            }
        }

        private static void DisplayErrors(string generationPhase, ErrorLog errorLog)
        {
            if (errorLog.Errors.Count > 0 || errorLog.FatalError != null)
            {
                Console.ForegroundColor = ErrorColor;
                Console.WriteLine();
                Console.WriteLine($"{generationPhase} FAILED with the following errors:");
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
            string sourceInfo = error.LineNumber >= 0 ? $" (File: {error.Filename}{lineInfo}{cfLineInfo})" : string.Empty;
            return $"{error.Message}{sourceInfo}";
        }
    }
}
