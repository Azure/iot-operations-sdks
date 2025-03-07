# Protocol compiler

The `Azure.IoT.Operations.ProtocolCompiler` takes a DTDL model file as an input, and outputs a server stub and client library in the requested languages.

## Install the compiler

1. Install [.NET](https://dotnet.microsoft.com/download)

1. Install the protocol compiler using the `dotnet` CLI:

    ```bash
    dotnet tool install -g Azure.IoT.Operations.ProtocolCompiler --add-source https://pkgs.dev.azure.com/azure-iot-sdks/iot-operations/_packaging/preview/nuget/v3/index.json
    ```

1. The compiler can now be executed using the command `Azure.Iot.Operations.ProtocolCompiler`

## Compiler options

The compiler provides the following options:

```bash
--modelFile <FILEPATH ...>  File(s) containing DTDL model(s) to process
--modelId <DTMI>            DTMI of Interface to use for codegen (not needed when model has only one Mqtt Interface)
--dmrRoot <DIRPATH | URL>   Directory or URL from which to retrieve referenced models
--workingDir <DIRPATH>      Directory for storing temporary files (relative to outDir unless path is rooted)
--outDir <DIRPATH>          Directory for receiving generated code [default: .]
--namespace <NAMESPACE>     Namespace for generated code (overrides namespace from model)
--lang <csharp|go|rust>     Programming language for generated code [default: csharp]
--clientOnly                Generate only client-side code
--serverOnly                Generate only server-side code
--noProj                    Do not generate code in a project
--defaultImpl               Generate default implementations of user-level callbacks
```

## Compilation scenarios

The following outlines the different options needed to resolve the model and its dependencies, depending where they are located.

### Model file with a single interface

If a model file is specified that only contains a single interface, then this interface will be used.

```bash
Azure.Iot.Operations.ProtocolCompiler --modelFile <FILEPATH ...>
```

### Model file with multiple interfaces

If a model file is specified containing multiple interfaces, then a model id is required to specify the interface to use using the `--modelId` option.

```bash
Azure.Iot.Operations.ProtocolCompiler --modelFile <FILEPATH ...> c
```

### Model with external dependencies

When the interface has dependencies on external interfaces, via `extends` or `components`, these dependencies can be resolved from a configurable DMR root (either URL or filesystem), following the [DMR conventions](https://github.com/Azure/iot-plugandplay-models-tools/wiki/Resolution-Convention).

```bash
Azure.Iot.Operations.ProtocolCompiler --modelFile <FILEPATH ...> --dmrRoot <DIRPATH | URL>
```

### Model from DMR

To use a model from a DMR, specify both the model id and the DMR root

```bash
Azure.Iot.Operations.ProtocolCompiler --modelId <DTMI> --dmrRoot <DIRPATH | URL>
```

## Model validation

The compiler utilized the [DTDLParser](https://www.nuget.org/packages/DTDLParser) and will output errors detected during parsing, such as:

* Invalid JSON
* On-DTDL file
* Invalid DTDL version
* MQTT Extension not defined
* PayloadFormat not specified
* TelemetryTopic should be defined if there are any Telemetry elements
* CommandTopic should be defined if there are any Command elements
