// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
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

    public static class CommandPerformer
    {
        public const string DefaultOutDir = ".";
        public const string DefaultWorkingDir = "schemas";

        public static readonly Dictionary<string, LanguageInfo> LanguageMap = new()
        {
            { "csharp", new LanguageInfo(TargetLanguage.CSharp, "", new Regex(@"^[A-Z][a-zA-Z0-9]*(?:\.[A-Z][a-zA-Z0-9]*)*$"), true, false, "Generated", "", "must have PascalCase name segments separated by dots") },
            { "rust", new LanguageInfo(TargetLanguage.Rust, "src", new Regex(@"^[a-z][a-z0-9]*(?:_[a-z][a-z0-9]*)*$"), true, true, "generated", "common_types", "must have a single snake_case name segment") },
            { "none", new LanguageInfo(TargetLanguage.None, "", new Regex(@"^$"), false, false, "", "", "must not be used") },
        };

        public static ErrorLog GenerateCode(OptionContainer options, Action<string, bool> statusReceiver, bool suppressExternalTools = false)
        {
            ErrorLog errorLog = new(options.WorkingDir.FullName);

            try
            {
                WarnOnSuspiciousOptions(options, errorLog);
                ValidateOptions(options, errorLog);
                if (errorLog.HasErrors)
                {
                    return errorLog;
                }

                string projectName = LegalizeProjectName(options.OutputDir.Name);
                LanguageInfo languageInfo = LanguageMap[options.Language.ToLowerInvariant()];

                string genNamespace = options.GenNamespace ?? languageInfo.DefaultNamespace;
                string commonNs = options.CommonNamespace ?? languageInfo.DefaultCommon;

                ValidateNamespaceOption(options.Language, genNamespace, "namespace", languageInfo.ArgRegex, languageInfo.NamespaceRequired, languageInfo.ArgConstraint, errorLog);
                ValidateNamespaceOption(options.Language, commonNs, "common", languageInfo.ArgRegex, languageInfo.CommonRequired, languageInfo.ArgConstraint, errorLog);
                if (errorLog.HasErrors)
                {
                    return errorLog;
                }

                string srcSubdir = options.NoProj ? string.Empty : languageInfo.SrcSubdir;

                errorLog.Phase = "Parsing";
                List<ParsedThing> parsedThings = new();
                HashSet<SerializationFormat> serializationFormats = new();
                ParseThings(options.ThingFiles, errorLog, statusReceiver, parsedThings, serializationFormats, options.PrefixSchemas, forClient: true, forServer: true);
                ParseThings(options.ClientThingFiles, errorLog, statusReceiver, parsedThings, serializationFormats, options.PrefixSchemas, forClient: true, forServer: false);
                ParseThings(options.ServerThingFiles, errorLog, statusReceiver, parsedThings, serializationFormats, options.PrefixSchemas, forClient: false, forServer: true);

                if (errorLog.HasErrors)
                {
                    return errorLog;
                }

                errorLog.Phase = "Schema generation";
                Dictionary<SerializationFormat, List<GeneratedItem>> generatedSchemas = SchemaGenerator.GenerateSchemas(parsedThings, projectName, options.WorkingDir);

                errorLog.CheckForDuplicatesInThings();
                if (errorLog.HasErrors)
                {
                    return errorLog;
                }

                foreach (List<GeneratedItem> schemas in generatedSchemas.Values)
                {
                    WriteItems(schemas, options.WorkingDir, statusReceiver);
                }

                if (languageInfo.TargetLanguage == TargetLanguage.None)
                {
                    statusReceiver?.Invoke("No code generation requested; exiting after schema generation.", false);
                    return errorLog;
                }

                FileInfo[] schemaFiles = options.SchemaFiles.SelectMany(fs => Directory.GetFiles(Path.GetDirectoryName(fs) ?? string.Empty, Path.GetFileName(fs)), (_, f) => new FileInfo(f)).ToArray();
                if (schemaFiles.Length > 0)
                {
                    ImportSchemas(schemaFiles, generatedSchemas);
                }

                string? typeNameInfoText = options.TypeNamerFile?.OpenText()?.ReadToEnd();
                TypeNamer typeNamer = new TypeNamer(typeNameInfoText);

                errorLog.Phase = "Type generation";
                List<GeneratedItem> generatedTypes = new();
                foreach (KeyValuePair<SerializationFormat, List<GeneratedItem>> schemaSet in generatedSchemas)
                {
                    Dictionary<string, string> schemaTextsByName = schemaSet.Value.ToDictionary(s => Path.GetFullPath(Path.Combine(options.WorkingDir.FullName, s.FolderPath, s.FileName)).Replace('\\', '/'), s => s.Content);
                    TypeGenerator typeGenerator = new TypeGenerator(schemaSet.Key, languageInfo.TargetLanguage, typeNamer, errorLog);
                    generatedTypes.AddRange(typeGenerator.GenerateTypes(schemaTextsByName, new MultiCodeName(genNamespace), new MultiCodeName(commonNs), projectName, srcSubdir));
                }

                errorLog.CheckForDuplicatesInSchemas();
                if (errorLog.HasErrors)
                {
                    return errorLog;
                }

                WriteItems(generatedTypes, options.OutputDir, statusReceiver);

                List<string> typeNames = generatedTypes.Select(gt => Path.GetFileNameWithoutExtension(gt.FileName)).ToList();

                string? sdkPath = options.SdkPath != null ? Path.GetRelativePath(options.OutputDir.FullName, options.SdkPath) : null;

                serializationFormats.UnionWith(generatedSchemas.Keys);

                errorLog.Phase = "Envoy generation";
                List<GeneratedItem> generatedEnvoys = EnvoyGenerator.GenerateEnvoys(
                    parsedThings,
                    serializationFormats.ToList(),
                    languageInfo.TargetLanguage,
                    genNamespace,
                    commonNs,
                    projectName,
                    sdkPath,
                    typeNames,
                    srcSubdir,
                    generateProject: !options.NoProj,
                    defaultImpl: options.DefaultImpl);
                WriteItems(generatedEnvoys, options.OutputDir, statusReceiver);

                if (languageInfo.TargetLanguage == TargetLanguage.Rust && !suppressExternalTools)
                {
                    GeneratedItem? cargoInfo = generatedEnvoys.FirstOrDefault(e => e.FileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase));
                    if (cargoInfo != null)
                    {
                        string projectFolder = Path.Combine(options.OutputDir.FullName, cargoInfo.FolderPath);
                        try
                        {
                            RunCargo($"fmt --manifest-path {Path.Combine(projectFolder, "Cargo.toml")}", display: true);
                            RunCargo("install --locked cargo-machete@0.7.0", display: false);
                            RunCargo($"machete --fix {projectFolder}", display: true);
                        }
                        catch (Win32Exception)
                        {
                            Console.WriteLine("cargo tool not found; install per instructions: https://doc.rust-lang.org/cargo/getting-started/installation.html");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddUnlocatableError(ErrorCondition.JsonInvalid, $"Exception: {ex.Message}", errorLog);
                return errorLog;
            }

            return errorLog;
        }

        private static void RunCargo(string args, bool display)
        {
            if (display)
            {
                Console.WriteLine($"cargo {args}");
            }

            using (Process cargo = new Process())
            {
                cargo.StartInfo.FileName = "cargo";
                cargo.StartInfo.Arguments = args;
                cargo.StartInfo.UseShellExecute = false;
                cargo.StartInfo.RedirectStandardOutput = true;
                cargo.Start();
                cargo.WaitForExit();
            }
        }

        private static void ParseThings(FileInfo[] thingFiles, ErrorLog errorLog, Action<string, bool> statusReceiver, List<ParsedThing> parsedThings, HashSet<SerializationFormat> serializationFormats, bool prefixSchemas, bool forClient, bool forServer)
        {
            foreach (FileInfo thingFile in thingFiles)
            {
                statusReceiver.Invoke($"Parsing Thing Model file: {thingFile.Name} ...", true);

                using (StreamReader thingReader = thingFile.OpenText())
                {
                    string thingText = thingReader.ReadToEnd();
                    byte[] thingBytes = Encoding.UTF8.GetBytes(thingText);
                    ErrorReporter errorReporter = new ErrorReporter(errorLog, thingFile.FullName, thingBytes);
                    ThingValidator thingValidator = new ThingValidator(errorReporter);

                    if (TryGetThings(errorReporter, thingBytes, out List<TDThing>? things))
                    {
                        int thingCount = 0;
                        foreach (TDThing thing in things)
                        {
                            if (thingValidator.TryValidateThing(thing, serializationFormats))
                            {
                                ValueTracker<StringHolder>? schemaNamesFilename = thing.Links?.Elements?.FirstOrDefault(l => l.Value.Rel?.Value.Value == TDValues.RelationSchemaNaming)?.Value.Href;
                                if (TryGetSchemaNamer(errorReporter, thingFile.DirectoryName!, schemaNamesFilename, prefixSchemas ? thing.Title?.Value.Value : null, out SchemaNamer? schemaNamer))
                                {
                                    thingCount++;
                                    parsedThings.Add(new ParsedThing(thing, thingFile.Name, thingFile.DirectoryName!, schemaNamer, errorReporter, forClient, forServer));
                                    errorReporter.RegisterNameOfThing(thing.Title!.Value.Value, thing.Title!.TokenIndex);
                                }
                            }
                        }

                        thingValidator.ValidateThingCollection(things);

                        statusReceiver.Invoke($" {thingCount} {(thingCount == 1 ? "TD" : "TDs")} validly parsed", false);
                    }
                }
            }
        }

        private static bool TryGetSchemaNamer(ErrorReporter errorReporter, string folderPath, ValueTracker<StringHolder>? namerFilename, string? schemaPrefix, [NotNullWhen(true)] out SchemaNamer? schemaNamer)
        {
            if (namerFilename == null)
            {
                schemaNamer = new SchemaNamer(schemaPrefix);
                return true;
            }

            FileInfo namerFile = new FileInfo(Path.Combine(folderPath, namerFilename.Value.Value));
            if (!namerFile.Exists)
            {
                errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Could not find schema naming file '{namerFilename.Value.Value}'.", namerFilename.TokenIndex);

                schemaNamer = null;
                return false;
            }

            string schemaNameInfoText = namerFile.OpenText().ReadToEnd();

            try
            {
                schemaNamer = new SchemaNamer(schemaPrefix, schemaNameInfoText);
                return true;
            }
            catch (Exception ex)
            {
                errorReporter.ReportError(ErrorCondition.JsonInvalid, $"Failed to parse schema naming file '{namerFilename.Value.Value}': {ex.Message}", namerFilename.TokenIndex);
                schemaNamer = null;
                return false;
            }
        }

        private static bool TryGetThings(ErrorReporter errorReporter, byte[] thingBytes, [NotNullWhen(true)] out List<TDThing>? things)
        {
            bool hasError = false;

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
                        errorReporter.ReportError(ErrorCondition.JsonInvalid, $"TD deserialization error: {tracker.DeserializationError ?? string.Empty}.", tracker.TokenIndex);
                        hasError = true;
                    }

                    if (item is ValueTracker<TDDataSchema> dataSchema && dataSchema.Value?.Ref != null)
                    {
                        errorReporter.RegisterReferenceFromThing(dataSchema.Value.Ref.TokenIndex, dataSchema.Value.Ref.Value.Value);
                    }
                    else if (item is ValueTracker<TDProperty> property && property.Value?.Ref != null)
                    {
                        errorReporter.RegisterReferenceFromThing(property.Value.Ref.TokenIndex, property.Value.Ref.Value.Value);
                    }
                }
            }

            return !hasError;
        }

        private static void WriteItems(List<GeneratedItem> generatedItems, DirectoryInfo destDir, Action<string, bool> statusReceiver)
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
                statusReceiver.Invoke($"  Generated {filePath}", false);
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

        private static void ValidateOptions(OptionContainer options, ErrorLog errorLog)
        {
            bool anyThingFiles = options.ThingFiles.Length + options.ClientThingFiles.Length + options.ServerThingFiles.Length > 0;
            bool anySchemaFiles = options.SchemaFiles.Any(fs => Directory.GetFiles(Path.GetDirectoryName(fs) ?? string.Empty, Path.GetFileName(fs)).Any());

            if (!anyThingFiles && !anySchemaFiles)
            {
                AddUnlocatableError(ErrorCondition.ElementMissing, $"no Thing Model files specified, and no schema files {(options.SchemaFiles.Length > 0 ? "found" : "specified")}.  Use option --help for CLI usage and options.", errorLog);
                return;
            }

            if (!LanguageMap.ContainsKey(options.Language))
            {
                string langCondition = string.IsNullOrEmpty(options.Language) ? "language not specified" : $"language '{options.Language}' not recognized";
                AddUnlocatableError(ErrorCondition.PropertyUnsupportedValue, $"{langCondition}; language must be {string.Join(" or ", LanguageMap.Keys.Select(l => $"'{l}'"))} (use 'none' for no code generation)", errorLog);
                return;
            }

            bool filesOverlap = false;
            foreach (FileInfo cf in options.ClientThingFiles)
            {
                if (options.ThingFiles.Any(tf => tf.FullName == cf.FullName))
                {
                    AddUnlocatableError(ErrorCondition.Duplication, $"Thing Model file '{cf.FullName}' in common thing list is duplicated in client thing list; remove file from --clientThings", errorLog);
                    filesOverlap = true;
                }
            }
            foreach (FileInfo sf in options.ServerThingFiles)
            {
                if (options.ThingFiles.Any(tf => tf.FullName == sf.FullName))
                {
                    AddUnlocatableError(ErrorCondition.Duplication, $"Thing Model file '{sf.FullName}' in common thing list is duplicated in server thing list; remove file from --serverThings", errorLog);
                    filesOverlap = true;
                }
            }
            foreach (FileInfo cf in options.ClientThingFiles)
            {
                if (options.ServerThingFiles.Any(sf => sf.FullName == cf.FullName))
                {
                    AddUnlocatableError(ErrorCondition.ValuesInconsistent, $"Thing Model file '{cf.FullName}' is in both client and server thing lists; for full generation, relocate file to --things", errorLog);
                    filesOverlap = true;
                }
            }
            if (filesOverlap)
            {
                return;
            }

            if (options.ThingFiles.Any(mf => !mf.Exists) || options.ClientThingFiles.Any(mf => !mf.Exists) || options.ServerThingFiles.Any(mf => !mf.Exists))
            {
                foreach (FileInfo f in options.ThingFiles.Where(tf => !tf.Exists))
                {
                    AddUnlocatableError(ErrorCondition.ItemNotFound, $"non-existent Thing Model file: {f.FullName}", errorLog);
                }
                foreach (FileInfo f in options.ClientThingFiles.Where(tf => !tf.Exists))
                {
                    AddUnlocatableError(ErrorCondition.ItemNotFound, $"non-existent client Thing Model file: {f.FullName}", errorLog);
                }
                foreach (FileInfo f in options.ServerThingFiles.Where(tf => !tf.Exists))
                {
                    AddUnlocatableError(ErrorCondition.ItemNotFound, $"non-existent server Thing Model file: {f.FullName}", errorLog);
                }
                return;
            }
        }

        private static void ValidateNamespaceOption(string language, string optNamespace, string optKey, Regex argRegex, bool argRequired, string argConstraint, ErrorLog errorLog)
        {
            if (optNamespace == string.Empty)
            {
                if (argRequired)
                {
                    AddUnlocatableError(ErrorCondition.ValuesInconsistent, $"for language {language}, the --{optKey} value must not be an empty string", errorLog);
                }
            }
            else if (!argRegex.IsMatch(optNamespace))
            {
                AddUnlocatableError(ErrorCondition.ValuesInconsistent, $"'{optNamespace}' is not a valid namespace for language {language}; the --{optKey} option {argConstraint}", errorLog);
            }
        }

        private static void WarnOnSuspiciousOptions(OptionContainer options, ErrorLog errorLog)
        {
            if (!options.WorkingDir.Exists)
            {
                WarnOnSuspiciousOption("workingDir", options.WorkingDir.Name, errorLog);
            }

            if (!options.OutputDir.Exists)
            {
                WarnOnSuspiciousOption("outDir", options.OutputDir.Name, errorLog);
            }

            WarnOnSuspiciousOption("namespace", options.GenNamespace, errorLog);
            WarnOnSuspiciousOption("sdkPath", options.SdkPath, errorLog);
        }

        private static void WarnOnSuspiciousOption(string optionName, string? pathName, ErrorLog errorLog)
        {
            if (pathName != null && pathName.StartsWith("--"))
            {
                AddUnlocatableWarning($"{optionName} \"{pathName}\" looks like a flag.  Did you forget to specify a value?", errorLog);
            }
        }

        private static void AddUnlocatableError(ErrorCondition condition, string message, ErrorLog errorLog)
        {
            errorLog.AddError(ErrorLevel.Error, condition, message, string.Empty, 0);
        }

        private static void AddUnlocatableWarning(string message, ErrorLog errorLog)
        {
            errorLog.AddError(ErrorLevel.Warning, ErrorCondition.None, message, string.Empty, 0);
        }
    }
}
