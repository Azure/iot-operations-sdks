# Protocol compiler

`Azure.IoT.Operations.ProtocolCompiler` requires a model file as an input `--modelFile`, this doc describes the expected behavior based on the input.

## Single File

* The tool should accept a single file
* The file should have a single interface (do not allow an array of DTDL interfaces)

## Resolving external dependencies

When the DTDL interface has dependencies on external interfaces, via `extends` or `components`, these dependencies can be resolved from a configurable DMR root, including the file system, following the [DMR conventions](https://github.com/Azure/iot-plugandplay-models-tools/wiki/Resolution-Convention).


The tool provides three options:

1. `--modelFile FILEPATH`
1. `--modelId DTMI`
1. `--dmrRoot DIRPATH | URL`

If the user specifies a model file and this file contains only one Interface, use this Interface for codegen.

If the user specifies a model file and this file contains more than one Interface, fail unless the user also specifies a model ID: "Model file contains more than one Interface; use --modelId to indicate which Interface should be used for code generation."

If the user does not specify a model file, require a model ID and a DMR root.
This supports:

very simple usage (model file containing one Interface)
slightly more involved (model file with multiple Interfaces and clear indication of which one to use for codegen)
no local file (DMR root and model ID)
combination (local file, DMR root, and model ID)

## Validation

The tool is using the `DTDLParser` and should output any errors detected during parsing, such as:

* Invalid JSON
* On-DTDL file
* Invalid DTDL version
* MQTT Extension not defined
* PayloadFormat not specified
* TelemetryTopic should be defined if there are any Telemetry elements
* CommandTopic should be defined if there are any Command elements
