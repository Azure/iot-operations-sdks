// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.CommandLine;
    using System.IO;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    internal class Program
    {
        static void Main(string[] args)
        {
            var thingsOption = new Option<FileInfo[]>(
                name: "--things",
                description: "File(s) containing WoT Thing Model(s) to process for full generation")
            { ArgumentHelpName = "FILEPATH ...", AllowMultipleArgumentsPerToken = true };

            var clientThingsOption = new Option<FileInfo[]>(
                name: "--clientThings",
                description: "File(s) containing WoT Thing Model(s) to process for client-side generation")
            { ArgumentHelpName = "FILEPATH ...", AllowMultipleArgumentsPerToken = true };

            var serverThingsOption = new Option<FileInfo[]>(
                name: "--serverThings",
                description: "File(s) containing WoT Thing Model(s) to process for server-side generation")
            { ArgumentHelpName = "FILEPATH ...", AllowMultipleArgumentsPerToken = true };

            var schemasOption = new Option<string[]>(
                name: "--schemas",
                description: "Filespec(s) of files containing schema definitions (each may include wildcards).")
            { ArgumentHelpName = "FILESPEC ...", AllowMultipleArgumentsPerToken = true };

            var typeNamerOption = new Option<FileInfo?>(
                name: "--typeNamer",
                description: "File containing JSON config for deriving type names from JSON Schema names")
            { ArgumentHelpName = "FILEPATH" };

            var outDirOption = new Option<DirectoryInfo>(
                name: "--outDir",
                getDefaultValue: () => new DirectoryInfo(CommandPerformer.DefaultOutDir),
                description: "Directory for receiving generated code")
            { ArgumentHelpName = "DIRPATH" };

            var workingDirOption = new Option<string>(
                name: "--workingDir",
                getDefaultValue: () => CommandPerformer.DefaultWorkingDir,
                description: "Directory for storing temporary files (relative to outDir unless path is rooted)")
            { ArgumentHelpName = "DIRPATH" };

            var namespaceOption = new Option<string>(
                name: "--namespace",
                getDefaultValue: () => CommandPerformer.DefaultNamespace,
                description: "Namespace for generated code")
            { ArgumentHelpName = "NAMESPACE" };

            var sdkPathOption = new Option<string?>(
                name: "--sdkPath",
                description: "Local path or feed URL for Azure.Iot.Operations.Protocol SDK")
            { ArgumentHelpName = "FILEPATH | URL" };

            var langOption = new Option<string>(
                name: "--lang",
                description: "Programming language for generated code")
            { ArgumentHelpName = string.Join('|', CommandHandler.SupportedLanguages) };

            var noProjOption = new Option<bool>(
                name: "--noProj",
                description: "Do not generate code in a project");

            var defaultImplOption = new Option<bool>(
                name: "--defaultImpl",
                description: "Generate default implementations of user-level callbacks");

            var rootCommand = new RootCommand("Akri MQTT code generation tool for WoT Thing Models")
        {
            thingsOption,
            clientThingsOption,
            serverThingsOption,
            schemasOption,
            typeNamerOption,
            outDirOption,
            workingDirOption,
            namespaceOption,
            sdkPathOption,
            langOption,
            noProjOption,
            defaultImplOption,
        };

            ArgBinder argBinder = new ArgBinder(
                thingsOption,
                clientThingsOption,
                serverThingsOption,
                schemasOption,
                typeNamerOption,
                outDirOption,
                workingDirOption,
                namespaceOption,
                sdkPathOption,
                langOption,
                noProjOption,
                defaultImplOption);

            rootCommand.SetHandler(
                (OptionContainer options) => { Environment.ExitCode = CommandHandler.GenerateCode(options); },
                argBinder);

            rootCommand.Invoke(args);
        }
    }
}
