set -e

gen=../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen 

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang go
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang go
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang go
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang go
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang go
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang go

go build ./...
