set -e

[[ -d ./CommandVariantsSample ]] && rm -r ./CommandVariantsSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --defaultImpl --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./CommandComplexSchemasSample ]] && rm -r ./CommandComplexSchemasSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./CommandRawSample ]] && rm -r ./CommandRawSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSample ]] && rm -r ./TelemetryAndCommandSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSampleFromSchema ]] && rm -r ./TelemetryAndCommandSampleFromSchema
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --namespace TelemetryAndCommand --workingDir ../TelemetryAndCommandSample/obj/Akri --outDir ./TelemetryAndCommandSampleFromSchema --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryAndCommandSampleClientOnly ]] && rm -r ./TelemetryAndCommandSampleClientOnly
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleClientOnly --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --clientOnly

[[ -d ./TelemetryAndCommandSampleServerOnly ]] && rm -r ./TelemetryAndCommandSampleServerOnly
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSampleServerOnly --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol --serverOnly

[[ -d ./TelemetryComplexSchemasSample ]] && rm -r ./TelemetryComplexSchemasSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryPrimitiveSchemasSample ]] && rm -r ./TelemetryPrimitiveSchemasSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryRawSingleSample ]] && rm -r ./TelemetryRawSingleSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryRawSingle.json --outDir ./TelemetryRawSingleSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./TelemetryRawSeparateSample ]] && rm -r ./TelemetryRawSeparateSample
../../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../dtdl/TelemetryRawSeparate.json --outDir ./TelemetryRawSeparateSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
