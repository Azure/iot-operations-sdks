// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Help;
    using System.IO;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    internal class Program
    {
        static void Main(string[] args)
        {
            var thingsOption = new Option<FileInfo[]>("--things")
            {
                Description = "File(s) containing WoT Thing Model(s) to process for full generation",
                HelpName = "FILEPATH ...",
                AllowMultipleArgumentsPerToken = true,
            };

            var clientThingsOption = new Option<FileInfo[]>("--clientThings")
            {
                Description = "File(s) containing WoT Thing Model(s) to process for client-side generation",
                HelpName = "FILEPATH ...",
                AllowMultipleArgumentsPerToken = true,
            };

            var serverThingsOption = new Option<FileInfo[]>("--serverThings")
            {
                Description = "File(s) containing WoT Thing Model(s) to process for server-side generation",
                HelpName = "FILEPATH ...",
                AllowMultipleArgumentsPerToken = true,
            };

            var schemasOption = new Option<string[]>("--schemas")
            {
                Description = "Filespec(s) of files containing schema definitions (each may include wildcards)",
                HelpName = "FILESPEC ...",
                AllowMultipleArgumentsPerToken = true,
            };

            var typeNamerOption = new Option<FileInfo?>("--typeNamer")
            {
                Description = "File containing JSON config for deriving type names from JSON Schema names",
                HelpName = "FILEPATH",
            };

            var outDirOption = new Option<DirectoryInfo>("--outDir")
            {
                DefaultValueFactory = (_) => new DirectoryInfo(CommandPerformer.DefaultOutDir),
                Description = "Directory for receiving generated code",
                HelpName = "DIRPATH",
            };

            var workingDirOption = new Option<string>("--workingDir")
            {
                DefaultValueFactory = (_) => CommandPerformer.DefaultWorkingDir,
                Description = "Directory for storing temporary files (relative to outDir unless path is rooted)",
                HelpName = "DIRPATH",
            };

            var namespaceOption = new Option<string?>("--namespace")
            {
                Description = $"Namespace for generated code [{string.Join(", ", CommandPerformer.LanguageMap.Where(kv => kv.Value.TargetLanguage != TargetLanguage.None).Select(kv => $"{kv.Key} default: \"{kv.Value.DefaultNamespace}\""))}]",
                HelpName = "NAMESPACE",
            };

            var commonOption = new Option<string?>("--common")
            {
                Description = $"Namespace for common code [{string.Join(", ", CommandPerformer.LanguageMap.Where(kv => kv.Value.TargetLanguage != TargetLanguage.None).Select(kv => $"{kv.Key} default: \"{kv.Value.DefaultCommon}\""))}]",
                HelpName = "NAMESPACE",
            };

            var sdkPathOption = new Option<string?>("--sdkPath")
            {
                Description = "Local path or feed URL for Azure.Iot.Operations.Protocol SDK",
                HelpName = "FILEPATH | URL",
            };

            var langOption = new Option<string>("--lang")
            {
                Description = $"Programming language for generated code",
                HelpName = string.Join('|', CommandHandler.SupportedLanguages),
            };

            var prefixSchemasOption = new Option<bool>("--prefixSchemas")
            {
                Description = "Apply Thing Model prefixes to schema type names (to avoid collisions across Thing Models)",
            };

            var noProjOption = new Option<bool>("--noProj")
            {
                Description = "Do not generate code in a project",
            };

            var defaultImplOption = new Option<bool>("--defaultImpl")
            {
                Description = "Generate default implementations of user-level callbacks",
            };

            var rootCommand = new RootCommand("Akri MQTT code generation tool for WoT Thing Models");
            rootCommand.Add(thingsOption);
            rootCommand.Add(clientThingsOption);
            rootCommand.Add(serverThingsOption);
            rootCommand.Add(schemasOption);
            rootCommand.Add(typeNamerOption);
            rootCommand.Add(outDirOption);
            rootCommand.Add(workingDirOption);
            rootCommand.Add(namespaceOption);
            rootCommand.Add(commonOption);
            rootCommand.Add(sdkPathOption);
            rootCommand.Add(langOption);
            rootCommand.Add(prefixSchemasOption);
            rootCommand.Add(noProjOption);
            rootCommand.Add(defaultImplOption);

            int helpIndex = rootCommand.Options.Select(o => o is HelpOption).ToList().IndexOf(true);
            rootCommand.Options[helpIndex].Action = new CustomHelpAction(rootCommand.Options[helpIndex].Action);

            rootCommand.SetAction(parseResult =>
            {
                DirectoryInfo outputDir = parseResult.GetValue(outDirOption)!;
                string workingDir = parseResult.GetValue(workingDirOption)!;

                Environment.ExitCode = CommandHandler.GenerateCode(new OptionContainer
                {
                    ThingFiles = parseResult.GetValue(thingsOption)!,
                    ClientThingFiles = parseResult.GetValue(clientThingsOption)!,
                    ServerThingFiles = parseResult.GetValue(serverThingsOption)!,
                    SchemaFiles = parseResult.GetValue(schemasOption)!,
                    TypeNamerFile = parseResult.GetValue(typeNamerOption),
                    OutputDir = outputDir,
                    WorkingDir = Path.IsPathRooted(workingDir) ? new DirectoryInfo(workingDir) : new DirectoryInfo(Path.Combine(outputDir.FullName, workingDir)),
                    GenNamespace = parseResult.GetValue(namespaceOption)!,
                    CommonNamespace = parseResult.GetValue(commonOption)!,
                    SdkPath = parseResult.GetValue(sdkPathOption),
                    Language = parseResult.GetValue(langOption)!,
                    PrefixSchemas = parseResult.GetValue(prefixSchemasOption),
                    NoProj = parseResult.GetValue(noProjOption),
                    DefaultImpl = parseResult.GetValue(defaultImplOption),
                });
            });

            ParseResult parseResult = rootCommand.Parse(args);
            parseResult.Invoke();
        }
    }
}
