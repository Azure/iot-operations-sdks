namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.ComponentModel;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using DTDLParser;

    internal class CommandHandler
    {
        private static readonly Dictionary<string, LanguageInfo> LanguageInfos = new()
        {
            { "csharp", new LanguageInfo(TargetLanguage.CSharp, $"obj{Path.DirectorySeparatorChar}Akri", string.Empty, SupportsSharing: true) },
            { "go", new LanguageInfo(TargetLanguage.Go, $"akri", string.Empty, SupportsSharing: false) },
            { "rust", new LanguageInfo(TargetLanguage.Rust, $"target{Path.DirectorySeparatorChar}akri", "src", SupportsSharing: true) },
        };

        public static readonly string[] SupportedLanguages = LanguageInfos.Keys.ToArray();

        public static async Task<int> GenerateCode(OptionContainer options)
        {
            try
            {
                bool isModelSpecified = options.ModelFiles.Length > 0 || (options.ModelId != null && options.DmrRoot != null);
                if (!isModelSpecified && options.GenNamespace == null)
                {
#if DEBUG
                    Console.WriteLine("You must specify at least (a) one modelFile, (b) both a modelId and dmrRoot, or (c) a namespace.");
                    Console.WriteLine("Alternatives (a) and (b) will generate schema definitions and code from a DTDL model.");
                    Console.WriteLine("Alternative (c) will generate code from schema definitions in the workingDir, bypassing DTDL.");
#else
                    Console.WriteLine("You must specify at least one modelFile or both a modelId and dmrRoot.");
#endif
                    Console.WriteLine("Use option --help for a full list of options");
                    return 1;
                }

                WarnOnSuspiciousOption("workingDir", options.WorkingDir);
                WarnOnSuspiciousOption("sdkPath", options.SdkPath);
                WarnOnSuspiciousOption("namespace", options.GenNamespace);
                WarnOnSuspiciousOption("shared", options.SharedPrefix);
                if (!options.OutDir.Exists)
                {
                    WarnOnSuspiciousOption("outDir", options.OutDir.Name);
                }

                if (!SupportedLanguages.Contains(options.Lang))
                {
                    Console.WriteLine($"language \"{options.Lang}\" not recognized.  Language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
                    return 1;
                }

                if (options.ClientOnly && options.ServerOnly)
                {
                    Console.WriteLine("options --clientOnly and --serverOnly are mutually exclusive");
                    return 1;
                }

                if (options.SharedPrefix != null && !LanguageInfos[options.Lang].SupportsSharing)
                {
                    Console.WriteLine($"option --shared is not compatible with --lang {options.Lang}");
                    return 1;
                }

                CodeName? genNamespace = options.GenNamespace != null ? new(options.GenNamespace) : null;

                Dtmi? sharedDtmi = null;
                if (options.SharedPrefix != null && (!Dtmi.TryCreateDtmi(options.SharedPrefix, out sharedDtmi) || sharedDtmi.MajorVersion != 0))
                {
                    Console.WriteLine($"shared prefix \"{options.SharedPrefix}\" must parse as a valid versionless DTMI, e.g. 'dtmi:foo:bar'");
                    return 1;
                }

                CodeName? sharedPrefix = sharedDtmi != null ? new(sharedDtmi) : null;

                string genRoot = Path.Combine(options.OutDir.FullName, options.NoProj ? string.Empty : LanguageInfos[options.Lang].GenSubdir);
                string projectName = LegalizeName(options.OutDir.Name);

                string workingPathResolved =
                    options.WorkingDir == null ? Path.Combine(options.OutDir.FullName, LanguageInfos[options.Lang].DefaultWorkingPath) :
                    Path.IsPathRooted(options.WorkingDir) ? options.WorkingDir :
                    Path.Combine(options.OutDir.FullName, options.WorkingDir);
                DirectoryInfo workingDir = new(workingPathResolved);

                if (isModelSpecified)
                {
                    if (options.ModelFiles.Any(mf => !mf.Exists))
                    {
                        Console.WriteLine("All modelFiles must exist.  Non-existent files specified:");
                        foreach (FileInfo f in options.ModelFiles.Where(mf => !mf.Exists))
                        {
                            Console.WriteLine($"  {f.FullName}");
                        }
                        return 1;
                    }

                    Dtmi? modelDtmi = null;
                    if (options.ModelId != null && !Dtmi.TryCreateDtmi(options.ModelId, out modelDtmi))
                    {
                        Console.WriteLine($"modelId \"{options.ModelId}\" is not a valid DTMI");
                        return 1;
                    }

                    Uri? dmrUri = null;
                    if (options.DmrRoot != null)
                    {
                        if (!Uri.TryCreate(options.DmrRoot, UriKind.Absolute, out dmrUri))
                        {
                            if (Directory.Exists(options.DmrRoot))
                            {
                                dmrUri = new Uri(Path.GetFullPath(options.DmrRoot));
                            }
                            else
                            {
                                Console.WriteLine($"The dmrRoot {options.DmrRoot} must exist");
                                return 1;
                            }
                        }
                    }

                    string[] modelTexts = options.ModelFiles.Select(mf => mf.OpenText().ReadToEnd()).ToArray();
                    string[] modelNames = options.ModelFiles.Select(mf => mf.Name).ToArray();
                    ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(modelTexts, modelNames, modelDtmi, dmrUri, Console.WriteLine);

                    if (contextualizedInterface.InterfaceId == null)
                    {
                        return 1;
                    }

                    if (genNamespace == null)
                    {
                        genNamespace = new(contextualizedInterface.InterfaceId);
                    }

                    var modelParser = new ModelParser();

                    if (!SchemaGenerator.GenerateSchemas(contextualizedInterface.ModelDict!, contextualizedInterface.InterfaceId, contextualizedInterface.MqttVersion, projectName, workingDir, genNamespace, sharedPrefix))
                    {
                        return 1;
                    }
                }

                string schemaFolder = Path.Combine(workingDir.FullName, genNamespace!.GetFolderName(TargetLanguage.Independent));

                if (!Directory.Exists(schemaFolder))
                {
                    Console.WriteLine($"No '{genNamespace!.GetFolderName(TargetLanguage.Independent)}' folder found in working directory {workingDir.FullName}");
                    return 1;
                }

                foreach (string schemaFileName in Directory.GetFiles(schemaFolder))
                {
                    TypesGenerator.GenerateType(options.Lang, LanguageInfos[options.Lang].Language, projectName, schemaFileName, workingDir, genRoot, genNamespace!);
                }

                if (sharedPrefix != null)
                {
                    string sharedSchemaFolder = new(Path.Combine(workingDir.FullName, sharedPrefix.GetFolderName(TargetLanguage.Independent)));
                    if (Directory.Exists(sharedSchemaFolder))
                    {
                        foreach (string schemaFileName in Directory.GetFiles(sharedSchemaFolder))
                        {
                            TypesGenerator.GenerateType(options.Lang, LanguageInfos[options.Lang].Language, projectName, schemaFileName, workingDir, genRoot, sharedPrefix);
                        }
                    }
                }

                string[] annexFiles = Directory.GetFiles(schemaFolder, $"*.annex.json");
                switch (annexFiles.Length)
                {
                    case 0:
                        Console.WriteLine("No annex file present in working directory, so no envoy files generated");
                        break;
                    case 1:
                        EnvoyGenerator.GenerateEnvoys(options.Lang, projectName, annexFiles.First(), options.OutDir, workingDir, genRoot, genNamespace!, sharedPrefix, options.SdkPath, options.Sync, !options.ServerOnly, !options.ClientOnly, options.DefaultImpl, !options.NoProj);
                        break;
                    default:
                        Console.WriteLine("Multiple annex files in working directory. To generate envoy files, remove all but one annex file:");
                        foreach (string annexFile in annexFiles)
                        {
                            Console.WriteLine($"  {annexFile}");
                        }
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }

        private record LanguageInfo(TargetLanguage Language, string DefaultWorkingPath, string GenSubdir, bool SupportsSharing);

        private static string LegalizeName(string fsName)
        {
            return string.Join('.', fsName.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(s => (char.IsNumber(s[0]) ? "_" : "") + Regex.Replace(s, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant)));
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
