#!/bin/sh

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler

[[ -d ./CommandVariantsSample ]] && rm -r ./CommandVariantsSample
$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang go

[[ -d ./CommandComplexSchemasSample ]] && rm -r ./CommandComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang go

[[ -d ./CommandRawSample ]] && rm -r ./CommandRawSample
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang go

[[ -d ./TelemetryAndCommandSample ]] && rm -r ./TelemetryAndCommandSample
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang go

[[ -d ./TelemetryAndCommandSampleFromSchema ]] && rm -r ./TelemetryAndCommandSampleFromSchema
$gen --namespace TelemetryAndCommand --workingDir ../TelemetryAndCommandSample/akri --outDir ./TelemetryAndCommandSampleFromSchema --lang go

[[ -d ./TelemetryAndCommandSampleClientOnly ]] && rm -r ./TelemetryAndCommandSampleClientOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly --lang go --clientOnly

[[ -d ./TelemetryAndCommandSampleServerOnly ]] && rm -r ./TelemetryAndCommandSampleServerOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly --lang go --serverOnly

[[ -d ./TelemetryComplexSchemasSample ]] && rm -r ./TelemetryComplexSchemasSample
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang go

[[ -d ./TelemetryPrimitiveSchemasSample ]] && rm -r ./TelemetryPrimitiveSchemasSample
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --resolver ../dtdl/resolver.json --lang go

[[ -d ./TelemetryRecursiveSchemasSample ]] && rm -r ./TelemetryRecursiveSchemasSample
$gen --modelFile ../dtdl/TelemetryRecursiveSchemas.json --outDir ./TelemetryRecursiveSchemasSample --lang go

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample --lang go

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample --lang go
