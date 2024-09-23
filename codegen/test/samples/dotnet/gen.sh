../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/CommandVariants.json --outDir ./CommandVariantsSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/CommandComplexSchemas.json --outDir ./CommandComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/CommandRaw.json --outDir ./CommandRawSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/TelemetryAndCommand.json --outDir ./TelemetryAndCommandSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/TelemetryComplexSchemas.json --outDir ./TelemetryComplexSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol
../../../src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile ../dtdl/TelemetryPrimitiveSchemas.json --outDir ./TelemetryPrimitiveSchemasSample --lang csharp --sdkPath ../../../../dotnet/src/Azure.Iot.Operations.Protocol

dotnet build ./CommandVariantsSample
dotnet build ./CommandComplexSchemasSample
dotnet build ./CommandRawSample
dotnet build ./TelemetryAndCommandSample
dotnet build ./TelemetryComplexSchemasSample
dotnet build ./TelemetryPrimitiveSchemasSample
