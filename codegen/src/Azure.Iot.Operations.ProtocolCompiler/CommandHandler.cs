namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.ComponentModel;
    using System.Collections.Generic;
    using System.Diagnostics;
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
                    Console.WriteLine("All modelFiles must exist");
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
                            Console.WriteLine("The dmrRoot DIRPATH must exist");
                            return 1;
                        }
                    }
                }

                if (!SupportedLanguages.Contains(options.Lang))
                {
                    Console.WriteLine($"language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
                    return 1;
                }

                if (options.ClientOnly && options.ServerOnly)
                {
                    Console.WriteLine("options --clientOnly and --serverOnly are mutually exclusive");
                    return 1;
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

                string serviceName = SchemaGenerator.GenerateSchemas(contextualizedInterface.ModelDict!, contextualizedInterface.InterfaceId, contextualizedInterface.MqttVersion, projectName, workingDir, out string annexFile, out List<string> schemaFiles);

                string genNamespace = NameFormatter.DtmiToNamespace(contextualizedInterface.InterfaceId);
                bool genOrUpdateProj = ShouldGenerateOrUpdateProject(options.Lang, options.OutDir);
                string genRoot = GetGenRoot(options.Lang, options.OutDir, genOrUpdateProj, serviceName);

                HashSet<string> sourceFilePaths = new();
                HashSet<SchemaKind> distinctSchemaKinds = new();

                foreach (string schemaFileName in schemaFiles)
                {
                    TypesGenerator.GenerateType(options.Lang, projectName, schemaFileName, workingDir, genRoot, genNamespace, sourceFilePaths, distinctSchemaKinds);
                }

                EnvoyGenerator.GenerateEnvoys(options.Lang, projectName, annexFile, workingDir, genRoot, genNamespace, options.SdkPath, options.Sync, !options.ServerOnly, !options.ClientOnly, sourceFilePaths, distinctSchemaKinds, genOrUpdateProj);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }

        private static string GetGenRoot(string language, DirectoryInfo outDir, bool genOrUpdateProj, string serviceName)
        {
            return language != "rust" ? outDir.FullName : Path.Combine(outDir.FullName, $"{NamingSupport.ToSnakeCase(serviceName)}_gen", genOrUpdateProj ? "src" : string.Empty);
        }

        private static bool ShouldGenerateOrUpdateProject(string language, DirectoryInfo outDir)
        {
            switch (language)
            {
                case "csharp":
                    return true;
                case "go":
                    return false;
                case "rust":
                    DirectoryInfo testDir = new(outDir.FullName);
                    while (!testDir.Exists)
                    {
                        testDir = testDir.Parent!;
                    }

                    Directory.SetCurrentDirectory(outDir.FullName);

                    try
                    {
                        Process cargoProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "cargo",
                                Arguments = "read-manifest",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            },
                        };

                        cargoProcess.Start();
                        cargoProcess.WaitForExit();
                        return cargoProcess.ExitCode != 0;
                    }
                    catch (Win32Exception)
                    {
                        Console.WriteLine("cargo tool not found; install per instructions: https://doc.rust-lang.org/cargo/getting-started/installation.html");
                        Environment.Exit(1);
                        return false;
                    }
                default:
                    throw new Exception($"language '{language}' not recognized");
            }
        }
    }
}
