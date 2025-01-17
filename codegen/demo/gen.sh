set -e

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler 

[[ -d ./dotnet/ProtocolCompiler.Demo/JsonComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/JsonComm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./dotnet/ProtocolCompiler.Demo/JsonComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/AvroComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/AvroComm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./dotnet/ProtocolCompiler.Demo/AvroComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./rust/protocol_compiler_demo/json_comm ]] && rm -r ./rust/protocol_compiler_demo/json_comm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./rust/protocol_compiler_demo/json_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/avro_comm ]] && rm -r ./rust/protocol_compiler_demo/avro_comm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./rust/protocol_compiler_demo/avro_comm --lang rust --sdkPath ../../rust

[[ -d ./go/client/dtmi_codegen_communicationTest_jsonModel__1 ]] && rm -r ./go/client/dtmi_codegen_communicationTest_jsonModel__1
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/client --lang go --clientOnly

[[ -d ./go/server/dtmi_codegen_communicationTest_jsonModel__1 ]] && rm -r ./go/server/dtmi_codegen_communicationTest_jsonModel__1
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/server --lang go --serverOnly
