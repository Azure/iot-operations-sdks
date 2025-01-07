namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using DTDLParser;

    internal class CommandHandler
    {
        private static readonly Dictionary<string, string> DefaultWorkingPaths = new()
        {
            { "csharp", $"obj{Path.DirectorySeparatorChar}Akri" },
            { "go", $"akri" },
            { "rust", $"target{Path.DirectorySeparatorChar}akri" },
        };

        public static readonly string[] SupportedLanguages = DefaultWorkingPaths.Keys.ToArray();

        public static async Task<int> GenerateCode(OptionContainer options)
        {
            try
            {
                if (options.ModelFiles.Length == 0 && (options.ModelId == null || options.DmrRoot == null))
                {
                    Console.WriteLine("You must specify at least one modelFile or both a modelId and dmrRoot");
                    return 1;
                }

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

                WarnOnSuspiciousOption("workingDir", options.WorkingDir);
                WarnOnSuspiciousOption("sdkPath", options.SdkPath);
                if (!options.OutDir.Exists)
                {
                    WarnOnSuspiciousOption("outDir", options.OutDir.Name);
                }

                string[] modelTexts = options.ModelFiles.Select(mf => mf.OpenText().ReadToEnd()).ToArray();
                string[] modelNames = options.ModelFiles.Select(mf => mf.Name).ToArray();
                ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(modelTexts, modelNames, modelDtmi, dmrUri, Console.WriteLine);

                if (contextualizedInterface.InterfaceId == null)
                {
                    Environment.Exit(1);
                }

                var modelParser = new ModelParser();

                string projectName = Path.GetFileNameWithoutExtension(options.OutDir.FullName);

                string workingPathResolved =
                    options.WorkingDir == null ? Path.Combine(options.OutDir.FullName, DefaultWorkingPaths[options.Lang]) :
                    Path.IsPathRooted(options.WorkingDir) ? options.WorkingDir :
                    Path.Combine(options.OutDir.FullName, options.WorkingDir);
                DirectoryInfo workingDir = new(workingPathResolved);

                string genNamespace = NameFormatter.DtmiToNamespace(contextualizedInterface.InterfaceId);

                SchemaGenerator.GenerateSchemas(contextualizedInterface.ModelDict!, contextualizedInterface.InterfaceId, contextualizedInterface.MqttVersion, projectName, workingDir, out string annexFile, out List<string> schemaFiles);

                HashSet<string> sourceFilePaths = new();
                HashSet<SchemaKind> distinctSchemaKinds = new();

                foreach (string schemaFileName in schemaFiles)
                {
                    TypesGenerator.GenerateType(options.Lang, projectName, schemaFileName, workingDir, options.OutDir, genNamespace, sourceFilePaths, distinctSchemaKinds);
                }

                EnvoyGenerator.GenerateEnvoys(options.Lang, projectName, annexFile, workingDir, options.OutDir, genNamespace, options.SdkPath, options.Sync, !options.ServerOnly, !options.ClientOnly, sourceFilePaths, distinctSchemaKinds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
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
