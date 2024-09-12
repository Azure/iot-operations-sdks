set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariants --lang go
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommand --lang go
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemas --lang go
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemas --lang go

go build ./...
