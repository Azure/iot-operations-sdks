set -e

gen=../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen

$gen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang csharp --sdkPath ../../../../../lib/dotnet/src/Azure.Iot.Operations.Protocol
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang csharp --sdkPath ../../../../../lib/dotnet/src/Azure.Iot.Operations.Protocol
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang csharp --sdkPath ../../../../../lib/dotnet/src/Azure.Iot.Operations.Protocol
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang csharp --sdkPath ../../../../../lib/dotnet/src/Azure.Iot.Operations.Protocol

dotnet build ./CommandVariantsSample
dotnet build ./TelemetryAndCommandSample
dotnet build ./TelemetryComplexSchemasSample
dotnet build ./TelemetryPrimitiveSchemasSample
