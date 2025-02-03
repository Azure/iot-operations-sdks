gen=../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

$gen --modelFile TestModel.json --outDir TestModel

cp TestModel/TestModel/TestRequestSchema.g.cs ../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/
cp TestModel/TestModel/TestResponseSchema.g.cs ../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/

rm -r TestModel
