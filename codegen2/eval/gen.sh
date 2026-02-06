#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/CommandComplexSchemasSample ]] && rm -r dotnet/CommandComplexSchemasSample
$gen --things wot/CommandComplexSchemas.TM.json --outDir dotnet/CommandComplexSchemasSample --lang csharp --namespace CommandComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_complex_schemas_gen ]] && rm -r rust/command_complex_schemas_gen
$gen --things wot/CommandComplexSchemas.TM.json --outDir rust/command_complex_schemas_gen --lang rust --namespace command_complex_schemas --sdkPath ../../rust

[[ -d dotnet/CommandVariantsSample ]] && rm -r dotnet/CommandVariantsSample
$gen --things wot/CommandVariants.TM.json --outDir dotnet/CommandVariantsSample --lang csharp --namespace CommandVariants --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --defaultImpl

[[ -d rust/command_variants_gen ]] && rm -r rust/command_variants_gen
$gen --things wot/CommandVariants.TM.json --outDir rust/command_variants_gen --lang rust --namespace command_variants --sdkPath ../../rust

[[ -d dotnet/Counters ]] && rm -r dotnet/Counters
$gen --things wot/CounterCollection.TM.json --outDir dotnet/Counters --lang csharp --namespace CounterCollection --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/counters ]] && rm -r rust/counters
$gen --things wot/CounterCollection.TM.json --outDir rust/counters --lang rust --namespace counter_collection --sdkPath ../../rust

[[ -d dotnet/TelemetryComplexSchemasSample ]] && rm -r dotnet/TelemetryComplexSchemasSample
$gen --things wot/TelemetryComplexSchemas.TM.json --outDir dotnet/TelemetryComplexSchemasSample --lang csharp --namespace TelemetryComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_complex_schemas_gen ]] && rm -r rust/telemetry_complex_schemas_gen
$gen --things wot/TelemetryComplexSchemas.TM.json --outDir rust/telemetry_complex_schemas_gen --lang rust --namespace telemetry_complex_schemas --sdkPath ../../rust

[[ -d dotnet/TelemetryPrimitiveSchemasSample ]] && rm -r dotnet/TelemetryPrimitiveSchemasSample
$gen --things wot/TelemetryPrimitiveSchemas.TM.json --outDir dotnet/TelemetryPrimitiveSchemasSample --lang csharp --namespace TelemetryPrimitiveSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_primitive_schemas_gen ]] && rm -r rust/telemetry_primitive_schemas_gen
$gen --things wot/TelemetryPrimitiveSchemas.TM.json --outDir rust/telemetry_primitive_schemas_gen --lang rust --namespace telemetry_primitive_schemas --sdkPath ../../rust

[[ -d dotnet/PropertySeparateSample ]] && rm -r dotnet/PropertySeparateSample
$gen --things wot/PropertySeparate.TM.json --outDir dotnet/PropertySeparateSample --lang csharp --namespace PropertySeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_separate_gen ]] && rm -r rust/property_separate_gen
$gen --things wot/PropertySeparate.TM.json --outDir rust/property_separate_gen --lang rust --namespace property_separate --sdkPath ../../rust

[[ -d dotnet/PropertyTogetherSample ]] && rm -r dotnet/PropertyTogetherSample
$gen --things wot/PropertyTogether.TM.json --outDir dotnet/PropertyTogetherSample --lang csharp --namespace PropertyTogether --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_together_gen ]] && rm -r rust/property_together_gen
$gen --things wot/PropertyTogether.TM.json --outDir rust/property_together_gen --lang rust --namespace property_together --sdkPath ../../rust

[[ -d dotnet/TwoThingsSample ]] && rm -r dotnet/TwoThingsSample
$gen --things wot/TwoThings.TM.json --outDir dotnet/TwoThingsSample --lang csharp --namespace TwoThings --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/two_things_gen ]] && rm -r rust/two_things_gen
$gen --things wot/TwoThings.TM.json --outDir rust/two_things_gen --lang rust --namespace two_things --sdkPath ../../rust

[[ -d dotnet/ExternalSchemasSample ]] && rm -r dotnet/ExternalSchemasSample
$gen --things wot/ExternalSchemas.TM.json --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasSample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_gen ]] && rm -r rust/external_schemas_gen
$gen --things wot/ExternalSchemas.TM.json --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_gen --lang rust --namespace external_schemas --sdkPath ../../rust

[[ -d dotnet/ExternalSchemasOnlySample ]] && rm -r dotnet/ExternalSchemasOnlySample
$gen --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasOnlySample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_only_gen ]] && rm -r rust/external_schemas_only_gen
$gen --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_only_gen --lang rust --namespace external_schemas_only --sdkPath ../../rust
