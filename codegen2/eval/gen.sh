#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/CommandComplexSchemasSample ]] && rm -r dotnet/CommandComplexSchemasSample
$gen --thingFiles wot/CommandComplexSchemas.TD.json --outDir dotnet/CommandComplexSchemasSample --lang csharp --namespace CommandComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/command_complex_schemas_gen ]] && rm -r rust/command_complex_schemas_gen
$gen --thingFiles wot/CommandComplexSchemas.TD.json --outDir rust/command_complex_schemas_gen --lang rust --namespace CommandComplexSchemas --sdkPath ../../rust

[[ -d dotnet/CommandVariantsSample ]] && rm -r dotnet/CommandVariantsSample
$gen --thingFiles wot/CommandVariants.TD.json --outDir dotnet/CommandVariantsSample --lang csharp --namespace CommandVariants --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --defaultImpl

[[ -d rust/command_variants_gen ]] && rm -r rust/command_variants_gen
$gen --thingFiles wot/CommandVariants.TD.json --outDir rust/command_variants_gen --lang rust --namespace CommandVariants --sdkPath ../../rust

[[ -d dotnet/Counters ]] && rm -r dotnet/Counters
$gen --thingFiles wot/CounterCollection.TD.json --outDir dotnet/Counters --lang csharp --namespace CounterCollection --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/counters ]] && rm -r rust/counters
$gen --thingFiles wot/CounterCollection.TD.json --outDir rust/counters --lang rust --namespace CounterCollection --sdkPath ../../rust

[[ -d dotnet/TelemetryComplexSchemasSample ]] && rm -r dotnet/TelemetryComplexSchemasSample
$gen --thingFiles wot/TelemetryComplexSchemas.TD.json --outDir dotnet/TelemetryComplexSchemasSample --lang csharp --namespace TelemetryComplexSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_complex_schemas_gen ]] && rm -r rust/telemetry_complex_schemas_gen
$gen --thingFiles wot/TelemetryComplexSchemas.TD.json --outDir rust/telemetry_complex_schemas_gen --lang rust --namespace TelemetryComplexSchemas --sdkPath ../../rust

[[ -d dotnet/TelemetryPrimitiveSchemasSample ]] && rm -r dotnet/TelemetryPrimitiveSchemasSample
$gen --thingFiles wot/TelemetryPrimitiveSchemas.TD.json --outDir dotnet/TelemetryPrimitiveSchemasSample --lang csharp --namespace TelemetryPrimitiveSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_primitive_schemas_gen ]] && rm -r rust/telemetry_primitive_schemas_gen
$gen --thingFiles wot/TelemetryPrimitiveSchemas.TD.json --outDir rust/telemetry_primitive_schemas_gen --lang rust --namespace TelemetryPrimitiveSchemas --sdkPath ../../rust

[[ -d dotnet/PropertySeparateSample ]] && rm -r dotnet/PropertySeparateSample
$gen --thingFiles wot/PropertySeparate.TD.json --outDir dotnet/PropertySeparateSample --lang csharp --namespace PropertySeparate --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_separate_gen ]] && rm -r rust/property_separate_gen
$gen --thingFiles wot/PropertySeparate.TD.json --outDir rust/property_separate_gen --lang rust --namespace PropertySeparate --sdkPath ../../rust

[[ -d dotnet/PropertyTogetherSample ]] && rm -r dotnet/PropertyTogetherSample
$gen --thingFiles wot/PropertyTogether.TD.json --outDir dotnet/PropertyTogetherSample --lang csharp --namespace PropertyTogether --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/property_together_gen ]] && rm -r rust/property_together_gen
$gen --thingFiles wot/PropertyTogether.TD.json --outDir rust/property_together_gen --lang rust --namespace PropertyTogether --sdkPath ../../rust

[[ -d dotnet/TwoThingsSample ]] && rm -r dotnet/TwoThingsSample
$gen --thingFiles wot/TwoThings.TD.json --outDir dotnet/TwoThingsSample --lang csharp --namespace TwoThings --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/two_things_gen ]] && rm -r rust/two_things_gen
$gen --thingFiles wot/TwoThings.TD.json --outDir rust/two_things_gen --lang rust --namespace TwoThings --sdkPath ../../rust

[[ -d dotnet/ExternalSchemasSample ]] && rm -r dotnet/ExternalSchemasSample
$gen --thingFiles wot/ExternalSchemas.TD.json --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasSample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_gen ]] && rm -r rust/external_schemas_gen
$gen --thingFiles wot/ExternalSchemas.TD.json --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_gen --lang rust --namespace ExternalSchemas --sdkPath ../../rust

[[ -d dotnet/ExternalSchemasOnlySample ]] && rm -r dotnet/ExternalSchemasOnlySample
$gen --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasOnlySample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_only_gen ]] && rm -r rust/external_schemas_only_gen
$gen --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_only_gen --lang rust --namespace ExternalSchemas --sdkPath ../../rust
