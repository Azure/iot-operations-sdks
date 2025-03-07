set -e

gen=../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler

[[ -d ./CommandVariantsSample ]] && rm -r ./CommandVariantsSample
$gen --defaultImpl --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./CommandComplexSchemasSample ]] && rm -r ./CommandComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./CommandRawSample ]] && rm -r ./CommandRawSample
$gen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSample ]] && rm -r ./TelemetryAndCommandSample
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSampleFromSchema ]] && rm -r ./TelemetryAndCommandSampleFromSchema
$gen --namespace TelemetryAndCommand --workingDir ../TelemetryAndCommandSample/obj/Akri --outDir ./TelemetryAndCommandSampleFromSchema --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSampleClientOnly ]] && rm -r ./TelemetryAndCommandSampleClientOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --clientOnly

[[ -d ./TelemetryAndCommandSampleServerOnly ]] && rm -r ./TelemetryAndCommandSampleServerOnly
$gen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --serverOnly

[[ -d ./TelemetryComplexSchemasSample ]] && rm -r ./TelemetryComplexSchemasSample
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryPrimitiveSchemasSample ]] && rm -r ./TelemetryPrimitiveSchemasSample
$gen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
$gen --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
$gen --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./SharedComplexSchemasSample ]] && rm -r ./SharedComplexSchemasSample
$gen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./SharedComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --shared dtmi:sharedSchemas
$gen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./SharedComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --shared dtmi:sharedSchemas
