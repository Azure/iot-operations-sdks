#!/bin/sh

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler

[[ -d ./CommandVariantsSample ]] && rm -r ./CommandVariantsSample
$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample/command_variants_gen --lang rust --sdkPath ../../../../rust

[[ -d ./CommandComplexSchemasSample ]] && rm -r ./CommandComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample/command_complex_schemas_gen --lang rust --sdkPath ../../../../rust

[[ -d ./CommandRawSample ]] && rm -r ./CommandRawSample
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample/command_raw_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandSample ]] && rm -r ./TelemetryAndCommandSample
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandSampleFromSchema ]] && rm -r ./TelemetryAndCommandSampleFromSchema
$gen --namespace TelemetryAndCommand --workingDir ../../TelemetryAndCommandSample/telemetry_and_command_gen/target/akri --outDir ./TelemetryAndCommandSampleFromSchema/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandSampleClientOnly ]] && rm -r ./TelemetryAndCommandSampleClientOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust --clientOnly

[[ -d ./TelemetryAndCommandSampleServerOnly ]] && rm -r ./TelemetryAndCommandSampleServerOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust --serverOnly

[[ -d ./TelemetryComplexSchemasSample ]] && rm -r ./TelemetryComplexSchemasSample
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample/telemetry_complex_schemas_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryPrimitiveSchemasSample ]] && rm -r ./TelemetryPrimitiveSchemasSample
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample/telemetry_primitive_schemas_gen --resolver ../dtdl/resolver.json --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample/telemetry_raw_single_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample/telemetry_raw_separate_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandNestedRaw ]] && rm -r ./TelemetryAndCommandNestedRaw
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandNestedRaw/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./TelemetryAndCommandNestedRaw/telemetry_and_command_gen/src/command_raw_gen --lang rust --noProj --sdkPath ../../../../rust

[[ -d ./SharedComplexSchemasSample ]] && rm -r ./SharedComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./SharedComplexSchemasSample/shared_complex_schemas_gen --lang rust --sdkPath ../../../../rust --shared dtmi:sharedSchemas
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./SharedComplexSchemasSample/shared_complex_schemas_gen --lang rust --sdkPath ../../../../rust --shared dtmi:sharedSchemas

[[ -d ./ComplexTypeSchemaSample ]] && rm -r ./ComplexTypeSchemaSample
mkdir ./ComplexTypeSchemaSample
mkdir ./ComplexTypeSchemaSample/complex_type_schema_gen
mkdir ./ComplexTypeSchemaSample/complex_type_schema_gen/target
mkdir ./ComplexTypeSchemaSample/complex_type_schema_gen/target/Akri
mkdir ./ComplexTypeSchemaSample/complex_type_schema_gen/target/Akri/ComplexTypeSchema
cp ../json/complex-type-schema.schema.json ./ComplexTypeSchemaSample/complex_type_schema_gen/target/Akri/ComplexTypeSchema
$gen --namespace ComplexTypeSchema --outDir ./ComplexTypeSchemaSample/complex_type_schema_gen --lang rust --sdkPath ../../../../rust
