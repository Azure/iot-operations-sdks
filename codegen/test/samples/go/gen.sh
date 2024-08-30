set -e

gen=../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen 

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariants --lang go
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommand --lang go
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemas --lang go
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemas --lang go

go build ./...
