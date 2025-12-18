#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/CommandComplexSchemasSample ]] && rm -r dotnet/CommandComplexSchemasSample
$gen --thingFiles conv/CommandComplexSchemas.TD.json --outDir dotnet/CommandComplexSchemasSample --lang csharp --namespace CommandComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_complex_schemas_gen ]] && rm -r rust/command_complex_schemas_gen
$gen --thingFiles conv/CommandComplexSchemas.TD.json --outDir rust/command_complex_schemas_gen --lang rust --namespace CommandComplexSchemas --sdkPath ../../rust

[[ -d dotnet/CommandRawSample ]] && rm -r dotnet/CommandRawSample
$gen --thingFiles conv/CommandRaw.TD.json --outDir dotnet/CommandRawSample --lang csharp --namespace CommandRaw --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_raw_gen ]] && rm -r rust/command_raw_gen
$gen --thingFiles conv/CommandRaw.TD.json --outDir rust/command_raw_gen --lang rust --namespace CommandRaw --sdkPath ../../rust

[[ -d dotnet/CommandVariantsSample ]] && rm -r dotnet/CommandVariantsSample
$gen --thingFiles conv/CommandVariants.TD.json --outDir dotnet/CommandVariantsSample --lang csharp --namespace CommandVariants --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --defaultImpl

[[ -d rust/command_variants_gen ]] && rm -r rust/command_variants_gen
$gen --thingFiles conv/CommandVariants.TD.json --outDir rust/command_variants_gen --lang rust --namespace CommandVariants --sdkPath ../../rust

[[ -d dotnet/PropertySeparateSample ]] && rm -r dotnet/PropertySeparateSample
$gen --thingFiles conv/PropertySeparate.TD.json --outDir dotnet/PropertySeparateSample --lang csharp --namespace PropertySeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_separate_gen ]] && rm -r rust/property_separate_gen
$gen --thingFiles conv/PropertySeparate.TD.json --outDir rust/property_separate_gen --lang rust --namespace PropertySeparate --sdkPath ../../rust

[[ -d dotnet/PropertyTogetherSample ]] && rm -r dotnet/PropertyTogetherSample
$gen --thingFiles conv/PropertyTogether.TD.json --outDir dotnet/PropertyTogetherSample --lang csharp --namespace PropertyTogether --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_together_gen ]] && rm -r rust/property_together_gen
$gen --thingFiles conv/PropertyTogether.TD.json --outDir rust/property_together_gen --lang rust --namespace PropertyTogether --sdkPath ../../rust

[[ -d dotnet/TelemetryAndCommandSample ]] && rm -r dotnet/TelemetryAndCommandSample
$gen --thingFiles conv/TelemetryAndCommand.TD.json --outDir dotnet/TelemetryAndCommandSample --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_and_command_gen ]] && rm -r rust/telemetry_and_command_gen
$gen --thingFiles conv/TelemetryAndCommand.TD.json --outDir rust/telemetry_and_command_gen --lang rust --namespace TelemetryAndCommand --sdkPath ../../rust

[[ -d dotnet/TelemetryComplexSchemasSample ]] && rm -r dotnet/TelemetryComplexSchemasSample
$gen --thingFiles conv/TelemetryComplexSchemas.TD.json --outDir dotnet/TelemetryComplexSchemasSample --lang csharp --namespace TelemetryComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_complex_schemas_gen ]] && rm -r rust/telemetry_complex_schemas_gen
$gen --thingFiles conv/TelemetryComplexSchemas.TD.json --outDir rust/telemetry_complex_schemas_gen --lang rust --namespace TelemetryComplexSchemas --sdkPath ../../rust

[[ -d dotnet/TelemetryPrimitiveSchemasSample ]] && rm -r dotnet/TelemetryPrimitiveSchemasSample
$gen --thingFiles conv/TelemetryPrimitiveSchemas.TD.json --outDir dotnet/TelemetryPrimitiveSchemasSample --lang csharp --namespace TelemetryPrimitiveSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_primitive_schemas_gen ]] && rm -r rust/telemetry_primitive_schemas_gen
$gen --thingFiles conv/TelemetryPrimitiveSchemas.TD.json --outDir rust/telemetry_primitive_schemas_gen --lang rust --namespace TelemetryPrimitiveSchemas --sdkPath ../../rust

[[ -d dotnet/TelemetryRawSeparateSample ]] && rm -r dotnet/TelemetryRawSeparateSample
$gen --thingFiles conv/TelemetryRawSeparate.TD.json --outDir dotnet/TelemetryRawSeparateSample --lang csharp --namespace TelemetryRawSeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_raw_separate_gen ]] && rm -r rust/telemetry_raw_separate_gen
$gen --thingFiles conv/TelemetryRawSeparate.TD.json --outDir rust/telemetry_raw_separate_gen --lang rust --namespace TelemetryRawSeparate --sdkPath ../../rust
