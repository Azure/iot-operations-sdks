#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/CommandComplexSchemasSample ]] && rm -r dotnet/CommandComplexSchemasSample
$gen --thingFiles conv/CommandComplexSchemas.TM.json --outDir dotnet/CommandComplexSchemasSample --lang csharp --namespace CommandComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_complex_schemas_gen ]] && rm -r rust/command_complex_schemas_gen
$gen --thingFiles conv/CommandComplexSchemas.TM.json --outDir rust/command_complex_schemas_gen --lang rust --namespace CommandComplexSchemas --sdkPath ../../rust

[[ -d dotnet/CommandRawSample ]] && rm -r dotnet/CommandRawSample
$gen --thingFiles conv/CommandRaw.TM.json --outDir dotnet/CommandRawSample --lang csharp --namespace CommandRaw --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_raw_gen ]] && rm -r rust/command_raw_gen
$gen --thingFiles conv/CommandRaw.TM.json --outDir rust/command_raw_gen --lang rust --namespace CommandRaw --sdkPath ../../rust

[[ -d dotnet/CommandVariantsSample ]] && rm -r dotnet/CommandVariantsSample
$gen --thingFiles conv/CommandVariants.TM.json --outDir dotnet/CommandVariantsSample --lang csharp --namespace CommandVariants --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --defaultImpl

[[ -d rust/command_variants_gen ]] && rm -r rust/command_variants_gen
$gen --thingFiles conv/CommandVariants.TM.json --outDir rust/command_variants_gen --lang rust --namespace CommandVariants --sdkPath ../../rust

[[ -d dotnet/PropertySeparateSample ]] && rm -r dotnet/PropertySeparateSample
$gen --thingFiles conv/PropertySeparate.TM.json --outDir dotnet/PropertySeparateSample --lang csharp --namespace PropertySeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_separate_gen ]] && rm -r rust/property_separate_gen
$gen --thingFiles conv/PropertySeparate.TM.json --outDir rust/property_separate_gen --lang rust --namespace PropertySeparate --sdkPath ../../rust

[[ -d dotnet/PropertyTogetherSample ]] && rm -r dotnet/PropertyTogetherSample
$gen --thingFiles conv/PropertyTogether.TM.json --outDir dotnet/PropertyTogetherSample --lang csharp --namespace PropertyTogether --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_together_gen ]] && rm -r rust/property_together_gen
$gen --thingFiles conv/PropertyTogether.TM.json --outDir rust/property_together_gen --lang rust --namespace PropertyTogether --sdkPath ../../rust

[[ -d dotnet/TelemetryAndCommandSample ]] && rm -r dotnet/TelemetryAndCommandSample
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSample --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_and_command_gen ]] && rm -r rust/telemetry_and_command_gen
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen --lang rust --namespace TelemetryAndCommand --sdkPath ../../rust

[[ -d dotnet/TelemetryAndCommandSampleClientOnly ]] && rm -r dotnet/TelemetryAndCommandSampleClientOnly
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSampleClientOnly --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --clientOnly

[[ -d rust/telemetry_and_command_gen_client_only ]] && rm -r rust/telemetry_and_command_gen_client_only
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen_client_only --lang rust --namespace TelemetryAndCommand --sdkPath ../../rust --clientOnly

[[ -d dotnet/TelemetryAndCommandSampleServerOnly ]] && rm -r dotnet/TelemetryAndCommandSampleServerOnly
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSampleServerOnly --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --serverOnly

[[ -d rust/telemetry_and_command_gen_server_only ]] && rm -r rust/telemetry_and_command_gen_server_only
$gen --thingFiles conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen_server_only --lang rust --namespace TelemetryAndCommand --sdkPath ../../rust --serverOnly

[[ -d dotnet/TelemetryComplexSchemasSample ]] && rm -r dotnet/TelemetryComplexSchemasSample
$gen --thingFiles conv/TelemetryComplexSchemas.TM.json --outDir dotnet/TelemetryComplexSchemasSample --lang csharp --namespace TelemetryComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_complex_schemas_gen ]] && rm -r rust/telemetry_complex_schemas_gen
$gen --thingFiles conv/TelemetryComplexSchemas.TM.json --outDir rust/telemetry_complex_schemas_gen --lang rust --namespace TelemetryComplexSchemas --sdkPath ../../rust

[[ -d dotnet/TelemetryPrimitiveSchemasSample ]] && rm -r dotnet/TelemetryPrimitiveSchemasSample
$gen --thingFiles conv/TelemetryPrimitiveSchemas.TM.json --outDir dotnet/TelemetryPrimitiveSchemasSample --lang csharp --namespace TelemetryPrimitiveSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_primitive_schemas_gen ]] && rm -r rust/telemetry_primitive_schemas_gen
$gen --thingFiles conv/TelemetryPrimitiveSchemas.TM.json --outDir rust/telemetry_primitive_schemas_gen --lang rust --namespace TelemetryPrimitiveSchemas --sdkPath ../../rust

[[ -d dotnet/TelemetryRawSeparateSample ]] && rm -r dotnet/TelemetryRawSeparateSample
$gen --thingFiles conv/TelemetryRawSeparate.TM.json --outDir dotnet/TelemetryRawSeparateSample --lang csharp --namespace TelemetryRawSeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_raw_separate_gen ]] && rm -r rust/telemetry_raw_separate_gen
$gen --thingFiles conv/TelemetryRawSeparate.TM.json --outDir rust/telemetry_raw_separate_gen --lang rust --namespace TelemetryRawSeparate --sdkPath ../../rust
