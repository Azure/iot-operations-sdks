gen=../../../tools/codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen 

$gen --modelFile TestModel.json --outDir TestModel

cp TestModel/dtmi_test_TestModel__1/Object_Test_Request.g.cs ../../dotnet/test/Azure.Iot.Operations.Protocol.UnitTests.Protocol/
cp TestModel/dtmi_test_TestModel__1/Object_Test_Response.g.cs ../../dotnet/test/Azure.Iot.Operations.Protocol.UnitTests.Protocol/

rm -r TestModel
