set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang go
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang go
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang go
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang go
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly --lang go --clientOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly --lang go --serverOnly
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang go
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang go
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample --lang go
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample --lang go

go build ./...
