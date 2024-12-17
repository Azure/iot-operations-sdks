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

        public static async Task<int> GenerateCode(FileInfo[] modelFiles, string? modelId, string? dmrRoot, string? workingPath, DirectoryInfo outDir, bool syncApi, string? sdkPath, string language)
        {
            try
            {
                if (modelFiles.Length == 0 && (modelId == null || dmrRoot == null))
                {
                    Console.WriteLine("You must specify at least one modelFile or both a modelId and dmrRoot");
                    return 1;
                }

                if (modelFiles.Any(mf => !mf.Exists))
                {
                    Console.WriteLine("All modelFiles must exist");
                    return 1;
                }

                Dtmi? modelDtmi = null;
                if (modelId != null && !Dtmi.TryCreateDtmi(modelId, out modelDtmi))
                {
                    Console.WriteLine($"modelId \"{modelId}\" is not a valid DTMI");
                    return 1;
                }

                Uri? dmrUri = null;
                if (dmrRoot != null)
                {
                    if (!Uri.TryCreate(dmrRoot, UriKind.Absolute, out dmrUri))
                    {
                        if (Directory.Exists(dmrRoot))
                        {
                            dmrUri = new Uri(Path.GetFullPath(dmrRoot));
                        }
                        else
                        {
                            Console.WriteLine("The dmrRoot DIRPATH must exist");
                            return 1;
                        }
                    }
                }

                if (!SupportedLanguages.Contains(language))
                {
                    Console.WriteLine($"language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
                    return 1;
                }

                string[] modelTexts = modelFiles.Select(mf => mf.OpenText().ReadToEnd()).ToArray();
                string[] modelNames = modelFiles.Select(mf => mf.Name).ToArray();
                ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(modelTexts, modelNames, modelDtmi, dmrUri, Console.WriteLine);

                if (contextualizedInterface.InterfaceId == null)
                {
                    Environment.Exit(1);
                }

                var modelParser = new ModelParser();

                string projectName = Path.GetFileNameWithoutExtension(outDir.FullName);

                string workingPathResolved =
                    workingPath == null ? Path.Combine(outDir.FullName, DefaultWorkingPaths[language]) :
                    Path.IsPathRooted(workingPath) ? workingPath :
                    Path.Combine(outDir.FullName, workingPath);
                DirectoryInfo workingDir = new(workingPathResolved);

                string genNamespace = NameFormatter.DtmiToNamespace(contextualizedInterface.InterfaceId);

                SchemaGenerator.GenerateSchemas(contextualizedInterface.ModelDict!, contextualizedInterface.InterfaceId, contextualizedInterface.MqttVersion, projectName, workingDir, out string annexFile, out List<string> schemaFiles);

                HashSet<string> sourceFilePaths = new();
                HashSet<SchemaKind> distinctSchemaKinds = new();

                foreach (string schemaFileName in schemaFiles)
                {
                    TypesGenerator.GenerateType(language, projectName, schemaFileName, workingDir, outDir, genNamespace, sourceFilePaths, distinctSchemaKinds);
                }

                EnvoyGenerator.GenerateEnvoys(language, projectName, annexFile, workingDir, outDir, genNamespace, sdkPath, syncApi, sourceFilePaths, distinctSchemaKinds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Code generation failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }
    }
}
