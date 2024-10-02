set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang rust --sdkPath ../../../../rust
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang rust --sdkPath ../../../../rust
