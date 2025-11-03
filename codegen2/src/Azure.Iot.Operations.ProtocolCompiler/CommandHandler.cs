namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.EnvoyGenerator;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;
    using Azure.Iot.Operations.TypeGenerator;
    using Azure.Iot.Operations.SchemaGenerator;

    internal class CommandHandler
    {
        private static readonly Dictionary<string, TargetLanguage> LanguageMap = new()
        {
            { "csharp", TargetLanguage.CSharp },
            { "rust", TargetLanguage.Rust },
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

                List<ParsedThing> parsedThings = ParseThings(options.ThingFiles);

                Dictionary<SerializationFormat, List<GeneratedItem>> generatedSchemas = SchemaGenerator.GenerateSchemas(parsedThings, projectName, options.WorkingDir);
                foreach (List<GeneratedItem> schemas in generatedSchemas.Values)
                {
                    WriteItems(schemas, options.WorkingDir);
                }

                FileInfo[] extSchemaFiles = options.ExtSchemaFiles.SelectMany(fs => Directory.GetFiles(Path.GetDirectoryName(fs) ?? string.Empty, Path.GetFileName(fs)), (_, f) => new FileInfo(f)).ToArray();
                if (extSchemaFiles.Length > 0)
                {
                    ImportSchemas(extSchemaFiles, generatedSchemas);
                }

                List<GeneratedItem> generatedTypes = new();
                foreach (KeyValuePair<SerializationFormat, List<GeneratedItem>> schemaSet in generatedSchemas)
                {
                    Dictionary<string, string> schemaTextsByName = schemaSet.Value.ToDictionary(s => s.FileName, s => s.Content);
                    TypeGenerator typeGenerator = new TypeGenerator(schemaSet.Key, targetLanguage);
                    generatedTypes.AddRange(typeGenerator.GenerateTypes(schemaTextsByName, new CodeName(options.GenNamespace), projectName, options.OutputSourceSubdir));
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }

        private static List<ParsedThing> ParseThings(FileInfo[] thingFiles)
        {
            List<ParsedThing> parsedThings = new();

            foreach (FileInfo thingFile in thingFiles)
            {
                Console.Write($"Parsing thing description file: {thingFile.Name} ...");

                using (StreamReader thingReader = thingFile.OpenText())
                {
                    string thingText = thingReader.ReadToEnd();
                    List<TDThing> things = TDParser.ParseMultiple(thingText);

                    foreach (TDThing thing in things)
                    {
                        string? schemaNamesFilename = thing.Links?.FirstOrDefault(l => l.Relation == TDValues.RelationSchemaNaming)?.Href;
                        string? schemaNameInfoText = schemaNamesFilename != null ? File.ReadAllText(Path.Combine(thingFile.DirectoryName!, schemaNamesFilename)) : null;
                        SchemaNamer schemaNamer = new SchemaNamer(schemaNameInfoText);

                        parsedThings.Add(new ParsedThing(thing, thingFile.DirectoryName!, schemaNamer));
                    }

                    Console.WriteLine($" {things.Count} {(things.Count == 1 ? "TD" : "TDs")} parsed");
                }
            }

            return parsedThings;
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
                    string n when n.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase) => SerializationFormat.Json,
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
            if (options.ThingFiles.Length == 0)
            {
                Console.WriteLine("No Thing Description files specified.");
                Console.WriteLine("Use option --help for CLI usage and options.");
                return false;
            }

            if (!SupportedLanguages.Contains(options.Language))
            {
                Console.WriteLine($"language \"{options.Language}\" not recognized.  Language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
                return false;
            }

            if (options.ClientOnly && options.ServerOnly)
            {
                Console.WriteLine("options --clientOnly and --serverOnly are mutually exclusive");
                return false;
            }

            if (options.ThingFiles.Any(mf => !mf.Exists))
            {
                Console.WriteLine("All Thing Description files must exist.  Non-existent files specified:");
                foreach (FileInfo f in options.ThingFiles.Where(tf => !tf.Exists))
                {
                    Console.WriteLine($"  {f.FullName}");
                }
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
                Console.WriteLine($"Warning: {optionName} \"{pathName}\" looks like a flag.  Did you forget to specify a value?");
            }
        }
    }
}
