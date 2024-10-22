gen=../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

$gen --modelFile TestModel.json --outDir TestModel

cp TestModel/dtmi_test_TestModel__1/Object_Test_Request.g.cs ../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/
cp TestModel/dtmi_test_TestModel__1/Object_Test_Response.g.cs ../../../../dotnet/test/Azure.Iot.Operations.Protocol.MetlTests/

rm -r TestModel
