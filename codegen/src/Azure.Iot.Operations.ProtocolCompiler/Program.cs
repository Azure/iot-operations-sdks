﻿using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Azure.Iot.Operations.ProtocolCompiler;

internal class Program
{
    private static readonly string DefaultOutDir = ".";
    private static readonly string DefaultLanguage = "csharp";

    static async Task Main(string[] args)
    {
        var modelFileOption = new Option<FileInfo[]>(
            name: "--modelFile",
            description: "File(s) containing DTDL model(s) to process")
            { ArgumentHelpName = "FILEPATH ...", AllowMultipleArgumentsPerToken = true };

        var modelIdOption = new Option<string?>(
            name: "--modelId",
            description: "DTMI of Interface to use for codegen (not needed when model has only one Mqtt Interface)")
            { ArgumentHelpName = "DTMI" };

        var dmrRootOption = new Option<string?>(
            name: "--dmrRoot",
            description: "Directory or URL from which to retrieve referenced models")
            { ArgumentHelpName = "DIRPATH | URL" };

        var workingDirOption = new Option<string?>(
            name: "--workingDir",
            description: "Directory for storing temporary files (relative to outDir unless path is rooted)")
            { ArgumentHelpName = "DIRPATH" };

        var outDirOption = new Option<DirectoryInfo>(
            name: "--outDir",
            getDefaultValue: () => new DirectoryInfo(DefaultOutDir),
            description: "Directory for receiving generated code")
            { ArgumentHelpName = "DIRPATH" };

#if DEBUG
        var syncOption = new Option<bool>(
            name: "--sync",
            description: "Generate synchronous API");

        var sdkPathOption = new Option<string?>(
            name: "--sdkPath",
            description: "Local path or feed URL for Azure.Iot.Operations.Protocol SDK")
            { ArgumentHelpName = "FILEPATH | URL" };
#endif

        var langOption = new Option<string>(
            name: "--lang",
            getDefaultValue: () => DefaultLanguage,
            description: "Programming language for generated code")
            { ArgumentHelpName = string.Join('|', CommandHandler.SupportedLanguages) };

        var clientOnlyOption = new Option<bool>(
            name: "--clientOnly",
            description: "Generate only client-side code");

        var serverOnlyOption = new Option<bool>(
            name: "--serverOnly",
            description: "Generate only server-side code");

        var rootCommand = new RootCommand("Akri MQTT code generation tool for DTDL models")
        {
            modelFileOption,
            modelIdOption,
            dmrRootOption,
            workingDirOption,
            outDirOption,
#if DEBUG
            syncOption,
            sdkPathOption,
#endif
            langOption,
            clientOnlyOption,
            serverOnlyOption,
        };


        ArgBinder argBinder = new ArgBinder(
            modelFileOption,
            modelIdOption,
            dmrRootOption,
            workingDirOption,
            outDirOption,
#if DEBUG
            syncOption,
            sdkPathOption,
#endif
            langOption,
            clientOnlyOption,
            serverOnlyOption);

        rootCommand.SetHandler(
            async (OptionContainer options) => { Environment.ExitCode = await CommandHandler.GenerateCode(options); },
            argBinder);

        await rootCommand.InvokeAsync(args);
    }
}
