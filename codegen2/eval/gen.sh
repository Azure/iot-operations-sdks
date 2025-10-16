#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/CommandComplexSchemasSample ]] && rm -r dotnet/CommandComplexSchemasSample
$gen --thingFiles wot/CommandComplexSchemas.TD.json --outDir dotnet/CommandComplexSchemasSample --lang csharp --namespace CommandComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_complex_schemas_gen ]] && rm -r rust/command_complex_schemas_gen
$gen --thingFiles wot/CommandComplexSchemas.TD.json --outDir rust/command_complex_schemas_gen --lang rust --namespace CommandComplexSchemas --sdkPath ../../rust --srcSubdir src

[[ -d dotnet/CommandVariantsSample ]] && rm -r dotnet/CommandVariantsSample
$gen --thingFiles wot/CommandVariants.TD.json --outDir dotnet/CommandVariantsSample --lang csharp --namespace CommandVariants --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_variants_gen ]] && rm -r rust/command_variants_gen
$gen --thingFiles wot/CommandVariants.TD.json --outDir rust/command_variants_gen --lang rust --namespace CommandVariants --sdkPath ../../rust --srcSubdir src

[[ -d dotnet/Counters ]] && rm -r dotnet/Counters
$gen --thingFiles wot/CounterCollection.TD.json --outDir dotnet/Counters --lang csharp --namespace CounterCollection --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/counters ]] && rm -r rust/counters
$gen --thingFiles wot/CounterCollection.TD.json --outDir rust/counters --lang rust --namespace CounterCollection --sdkPath ../../rust --srcSubdir src

[[ -d dotnet/TelemetryComplexSchemasSample ]] && rm -r dotnet/TelemetryComplexSchemasSample
$gen --thingFiles wot/TelemetryComplexSchemas.TD.json --outDir dotnet/TelemetryComplexSchemasSample --lang csharp --namespace TelemetryComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_complex_schemas_gen ]] && rm -r rust/telemetry_complex_schemas_gen
$gen --thingFiles wot/TelemetryComplexSchemas.TD.json --outDir rust/telemetry_complex_schemas_gen --lang rust --namespace TelemetryComplexSchemas --sdkPath ../../rust --srcSubdir src

[[ -d dotnet/TelemetryPrimitiveSchemasSample ]] && rm -r dotnet/TelemetryPrimitiveSchemasSample
$gen --thingFiles wot/TelemetryPrimitiveSchemas.TD.json --outDir dotnet/TelemetryPrimitiveSchemasSample --lang csharp --namespace TelemetryPrimitiveSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_primitive_schemas_gen ]] && rm -r rust/telemetry_primitive_schemas_gen
$gen --thingFiles wot/TelemetryPrimitiveSchemas.TD.json --outDir rust/telemetry_primitive_schemas_gen --lang rust --namespace TelemetryPrimitiveSchemas --sdkPath ../../rust --srcSubdir src
