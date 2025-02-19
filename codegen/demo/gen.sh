set -e

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler 

[[ -d ./dotnet/ProtocolCompiler.Demo/JsonComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/JsonComm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./dotnet/ProtocolCompiler.Demo/JsonComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/AvroComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/AvroComm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./dotnet/ProtocolCompiler.Demo/AvroComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/RawComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/RawComm
$gen --modelFile ./dtdl/RawModel.json --outDir ./dotnet/ProtocolCompiler.Demo/RawComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/CustomComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/CustomComm
$gen --modelFile ./dtdl/CustomModel.json --outDir ./dotnet/ProtocolCompiler.Demo/CustomComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./rust/protocol_compiler_demo/json_comm ]] && rm -r ./rust/protocol_compiler_demo/json_comm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./rust/protocol_compiler_demo/json_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/avro_comm ]] && rm -r ./rust/protocol_compiler_demo/avro_comm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./rust/protocol_compiler_demo/avro_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/raw_comm ]] && rm -r ./rust/protocol_compiler_demo/raw_comm
$gen --modelFile ./dtdl/RawModel.json --outDir ./rust/protocol_compiler_demo/raw_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/custom_comm ]] && rm -r ./rust/protocol_compiler_demo/custom_comm
$gen --modelFile ./dtdl/CustomModel.json --outDir ./rust/protocol_compiler_demo/custom_comm --lang rust --sdkPath ../../rust

[[ -d ./go/client/JsonModel ]] && rm -r ./go/client/JsonModel
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/client --lang go --clientOnly

[[ -d ./go/server/JsonModel ]] && rm -r ./go/server/JsonModel
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/server --lang go --serverOnly

[[ -d ./go/client/RawModel ]] && rm -r ./go/client/RawModel
$gen --modelFile ./dtdl/RawModel.json --outDir ./go/client --lang go --clientOnly

[[ -d ./go/server/RawModel ]] && rm -r ./go/server/RawModel
$gen --modelFile ./dtdl/RawModel.json --outDir ./go/server --lang go --serverOnly

[[ -d ./go/client/CustomModel ]] && rm -r ./go/client/CustomModel
$gen --modelFile ./dtdl/CustomModel.json --outDir ./go/client --lang go --clientOnly

[[ -d ./go/server/CustomModel ]] && rm -r ./go/server/CustomModel
$gen --modelFile ./dtdl/CustomModel.json --outDir ./go/server --lang go --serverOnly
