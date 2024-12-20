set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

[[ -d ./CommandVariantsSample ]] && rm -r ./CommandVariantsSample
$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang rust --sdkPath ../../../../rust

[[ -d ./CommandComplexSchemasSample ]] && rm -r ./CommandComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang rust --sdkPath ../../../../rust

[[ -d ./CommandRawSample ]] && rm -r ./CommandRawSample
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandSample ]] && rm -r ./TelemetryAndCommandSample
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandSampleClientOnly ]] && rm -r ./TelemetryAndCommandSampleClientOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly --lang rust --sdkPath ../../../../rust --clientOnly

[[ -d ./TelemetryAndCommandSampleServerOnly ]] && rm -r ./TelemetryAndCommandSampleServerOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly --lang rust --sdkPath ../../../../rust --serverOnly

[[ -d ./TelemetryComplexSchemasSample ]] && rm -r ./TelemetryComplexSchemasSample
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryPrimitiveSchemasSample ]] && rm -r ./TelemetryPrimitiveSchemasSample
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample --lang rust --sdkPath ../../../../rust

[[ -d ./TelemetryAndCommandNestedRaw ]] && rm -r ./TelemetryAndCommandNestedRaw
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandNestedRaw --lang rust
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./TelemetryAndCommandNestedRaw/telemetry_and_command_gen/src --lang rust
