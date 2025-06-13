using System.CommandLine;

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

        var workingDirOption = new Option<string?>(
            name: "--workingDir",
            description: "Directory for storing temporary files (relative to outDir unless path is rooted)")
            { ArgumentHelpName = "DIRPATH" };

        var outDirOption = new Option<DirectoryInfo>(
            name: "--outDir",
            getDefaultValue: () => new DirectoryInfo(DefaultOutDir),
            description: "Directory for receiving generated code (or TD file if --thingOnly is specified)")
            { ArgumentHelpName = "DIRPATH" };

        var resolverOption = new Option<FileInfo?>(
            name: "--resolver",
            description: "Path to a JSON file defining how to resolve referenced identifiers in models")
            { ArgumentHelpName = "FILEPATH" };

        var namespaceOption = new Option<string?>(
            name: "--namespace",
            description: "Namespace for generated code (overrides namespace from model or annex file; required if no model)")
            { ArgumentHelpName = "NAMESPACE" };

        var sharedOption = new Option<string?>(
            name: "--shared",
            description: "DTMI prefix of shared schemas")
            { ArgumentHelpName = "IDPREFIX" };

        var sdkPathOption = new Option<string?>(
            name: "--sdkPath",
            description: "Local path or feed URL for Azure.Iot.Operations.Protocol SDK")
            { ArgumentHelpName = "FILEPATH | URL" };

        var langOption = new Option<string>(
            name: "--lang",
            getDefaultValue: () => DefaultLanguage,
            description: "Programming language for generated code")
            { ArgumentHelpName = string.Join('|', CommandHandler.SupportedLanguages) };

        var thingOnlyOption = new Option<bool>(
            name: "--thingOnly",
            description: "Stop after generating Thing Description from DTDL model");

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

        var rootCommand = new RootCommand("Akri MQTT code generation tool for DTDL models")
        {
            modelFileOption,
            modelIdOption,
            workingDirOption,
            outDirOption,
            resolverOption,
            namespaceOption,
            sharedOption,
            sdkPathOption,
            langOption,
            thingOnlyOption,
            clientOnlyOption,
            serverOnlyOption,
            noProjOption,
            defaultImplOption,
        };

        ArgBinder argBinder = new ArgBinder(
            modelFileOption,
            modelIdOption,
            workingDirOption,
            outDirOption,
            resolverOption,
            namespaceOption,
            sharedOption,
            sdkPathOption,
            langOption,
            thingOnlyOption,
            clientOnlyOption,
            serverOnlyOption,
            noProjOption,
            defaultImplOption);

        rootCommand.SetHandler(
            async (OptionContainer options) => { Environment.ExitCode = await CommandHandler.GenerateCode(options); },
            argBinder);

        await rootCommand.InvokeAsync(args);
    }
}
