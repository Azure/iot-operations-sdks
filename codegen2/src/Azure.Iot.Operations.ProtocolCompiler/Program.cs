﻿namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.CommandLine;
    using System.IO;

    internal class Program
    {
        private static readonly string DefaultOutDir = ".";
        private static readonly string DefaultWorkingDir = "schemas";
        private static readonly string DefaultNamespace = "Generated";

        static void Main(string[] args)
        {
            var thingFilesOption = new Option<FileInfo[]>(
                name: "--thingFiles",
                description: "File(s) containing WoT Thing Description(s) to process")
            { ArgumentHelpName = "FILEPATH ...", AllowMultipleArgumentsPerToken = true };

            var extSchemasOption = new Option<string[]>(
                name: "--extSchemas",
                description: "Filespec(s) of files containing external schema definitions (each may include wildcards).")
            { ArgumentHelpName = "FILESPEC ...", AllowMultipleArgumentsPerToken = true };

            var outDirOption = new Option<DirectoryInfo>(
                name: "--outDir",
                getDefaultValue: () => new DirectoryInfo(DefaultOutDir),
                description: "Directory for receiving generated code")
            { ArgumentHelpName = "DIRPATH" };

            var workingDirOption = new Option<string>(
                name: "--workingDir",
                getDefaultValue: () => DefaultWorkingDir,
                description: "Directory for storing temporary files (relative to outDir unless path is rooted)")
            { ArgumentHelpName = "DIRPATH" };

            var srcSubdirOption = new Option<string>(
                name: "--srcSubdir",
                getDefaultValue: () => string.Empty,
                description: "Subdirectory under OutputDir for generated source code")
            { ArgumentHelpName = "SUBDIR" };

            var namespaceOption = new Option<string>(
                name: "--namespace",
                getDefaultValue: () => DefaultNamespace,
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

            var clientOnlyOption = new Option<bool>(
                name: "--clientOnly",
                description: "Generate only client-side code");

            var serverOnlyOption = new Option<bool>(
                name: "--serverOnly",
                description: "Generate only server-side code");

            var noProjOption = new Option<bool>(
                name: "--noProj",
                description: "Do not generate code in a project");

            var defaultImplOption = new Option<bool>(
                name: "--defaultImpl",
                description: "Generate default implementations of user-level callbacks");

            var rootCommand = new RootCommand("Akri MQTT code generation tool for WoT Thing Descriptions")
        {
            thingFilesOption,
            extSchemasOption,
            outDirOption,
            workingDirOption,
            srcSubdirOption,
            namespaceOption,
            sdkPathOption,
            langOption,
            clientOnlyOption,
            serverOnlyOption,
            noProjOption,
            defaultImplOption,
        };

            ArgBinder argBinder = new ArgBinder(
                thingFilesOption,
                extSchemasOption,
                outDirOption,
                workingDirOption,
                srcSubdirOption,
                namespaceOption,
                sdkPathOption,
                langOption,
                clientOnlyOption,
                serverOnlyOption,
                noProjOption,
                defaultImplOption);

            rootCommand.SetHandler(
                (OptionContainer options) => { Environment.ExitCode = CommandHandler.GenerateCode(options); },
                argBinder);

            rootCommand.Invoke(args);
        }
    }
}
