# ProtocolCompiler code-generation samples

This directory contains sample models and generation scripts that produce executable code samples in C#, Go, and Rust.

The top-level folders are:

* `dtdl` &mdash; contains sample DTDL model files
* `json` &mdash; contains a sample JSON input file
* `dotnet` &mdash; contains scripts that generate and build .NET code from files in the `dtdl` and `json` folders
* `go` &mdash; contains scripts that generate and build Go code from files in the `dtdl` folder
* `rust` &mdash; contains scripts that generate and build Rust code from files in the `dtdl` and `json` folders

## DotNet

To generate C# source files, run the `gen.sh` file from within the `dotnet` folder.
This will generate a bunch of subfolders containing generated C# code.
For example, the subfolder `TelemetryAndCommandSample` will contain:

* `dotnet/TelemetryAndCommandSample/obj/Akri/TelemetryAndCommand/*.schema.json` &mdash;  JSON Schema files generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `dotnet/TelemetryAndCommandSample/obj/Akri/TelemetryAndCommand/TelemetryAndCommand.annex.json` &mdash;  supplementary information file generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `dotnet/TelemetryAndCommandSample/obj/Akri/TelemetryAndCommand.TD.json` &mdash;  WoT Thing Description file translated from DTDL model `dtdl/TelemetryAndCommand.json`
* `dotnet/TelemetryAndCommandSample/TelemetryAndCommandSample.csproj` &mdash; .NET project file for generated code
* `dotnet/TelemetryAndCommandSample/*.cs` &mdash; common C# source files not derived from any DTDL model
* `dotnet/TelemetryAndCommandSample/TelemetryAndCommand/*.g.cs` &mdash;  C# source files generated from DTDL model `dtdl/TelemetryAndCommand.json`

To build the generated .NET samples, run the `bld.sh` file from within the `dotnet` folder.

## Go

To generate Go source files, run the `gen.sh` file from within the `go` folder.
This will generate a bunch of subfolders containing generated Go code.
For example, the subfolder `TelemetryAndCommandSample` will contain:

* `go/TelemetryAndCommandSample/akri/TelemetryAndCommand/*.schema.json` &mdash;  JSON Schema files generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `go/TelemetryAndCommandSample/akri/TelemetryAndCommand/TelemetryAndCommand.annex.json` &mdash;  supplementary information file generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `go/TelemetryAndCommandSample/akri/TelemetryAndCommand.TD.json` &mdash;  WoT Thing Description file translated from DTDL model `dtdl/TelemetryAndCommand.json`
* `go/TelemetryAndCommandSample/telemetryandcommand/*.go` &mdash;  Go source files generated from DTDL model `dtdl/TelemetryAndCommand.json`

To build the generated Go samples, run the `bld.sh` file from within the `go` folder.

## Rust

To generate Rust source files, run the `gen.sh` file from within the `rust` folder.
This will generate a bunch of subfolders containing generated Rust code.
For example, the subfolder `TelemetryAndCommandSample` will contain:

* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/target/akri/TelemetryAndCommand/*.schema.json` &mdash;  JSON Schema files generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/target/akri/TelemetryAndCommand/TelemetryAndCommand.annex.json` &mdash;  supplementary information file generated from DTDL model `dtdl/TelemetryAndCommand.json`
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/target/akri/TelemetryAndCommand.TD.json` &mdash;  WoT Thing Description file translated from DTDL model `dtdl/TelemetryAndCommand.json`
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/Cargo.toml` &mdash; Rust Cargo file for generated code
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/src/*.rs` &mdash; Rust top-level source files for generated code
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/src/common_types/*.rs` &mdash; common Rust source files not derived from any DTDL model
* `rust/TelemetryAndCommandSample/telemetry_and_command_gen/src/telemetry_and_command/*.rs` &mdash; source files generated from DTDL model `dtdl/TelemetryAndCommand.json`

To build the generated Rust samples, run the `bld.sh` file from within the `rust` folder.
