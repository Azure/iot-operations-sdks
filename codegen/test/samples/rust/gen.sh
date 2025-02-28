set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

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
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample/telemetry_primitive_schemas_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample/telemetry_raw_single_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample/telemetry_raw_separate_gen --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandNestedRaw ]] && rm -r ./TelemetryAndCommandNestedRaw
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandNestedRaw/telemetry_and_command_gen --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./TelemetryAndCommandNestedRaw/telemetry_and_command_gen/src/command_raw_gen --lang rust --noProj --sdkPath ../../../../rust
