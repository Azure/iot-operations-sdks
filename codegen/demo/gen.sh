#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler 

[[ -d ./dotnet/ProtocolCompiler.Demo/JsonComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/JsonComm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./dotnet/ProtocolCompiler.Demo/JsonComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/AvroComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/AvroComm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./dotnet/ProtocolCompiler.Demo/AvroComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/RawComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/RawComm
$gen --modelFile ./dtdl/RawModel.json --outDir ./dotnet/ProtocolCompiler.Demo/RawComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/CustomComm ]] && rm -r ./dotnet/ProtocolCompiler.Demo/CustomComm
$gen --modelFile ./dtdl/CustomModel.json --outDir ./dotnet/ProtocolCompiler.Demo/CustomComm --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.Demo/Counters ]] && rm -r ./dotnet/ProtocolCompiler.Demo/Counters
$gen --modelFile ./dtdl/CounterCollection.json --outDir ./dotnet/ProtocolCompiler.Demo/Counters --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./rust/protocol_compiler_demo/json_comm ]] && rm -r ./rust/protocol_compiler_demo/json_comm
$gen --modelFile ./dtdl/JsonModel.json --outDir ./rust/protocol_compiler_demo/json_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/avro_comm ]] && rm -r ./rust/protocol_compiler_demo/avro_comm
$gen --modelFile ./dtdl/AvroModel.json --outDir ./rust/protocol_compiler_demo/avro_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/raw_comm ]] && rm -r ./rust/protocol_compiler_demo/raw_comm
$gen --modelFile ./dtdl/RawModel.json --outDir ./rust/protocol_compiler_demo/raw_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/custom_comm ]] && rm -r ./rust/protocol_compiler_demo/custom_comm
$gen --modelFile ./dtdl/CustomModel.json --outDir ./rust/protocol_compiler_demo/custom_comm --lang rust --sdkPath ../../rust

[[ -d ./rust/protocol_compiler_demo/counters ]] && rm -r ./rust/protocol_compiler_demo/counters
$gen --modelFile ./dtdl/CounterCollection.json --outDir ./rust/protocol_compiler_demo/counters --lang rust --sdkPath ../../rust

[[ -d ./go/telemclient/JsonModel ]] && rm -r ./go/telemclient/JsonModel
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/telemclient --lang go --clientOnly

[[ -d ./go/telemserver/JsonModel ]] && rm -r ./go/telemserver/JsonModel
$gen --modelFile ./dtdl/JsonModel.json --outDir ./go/telemserver --lang go --serverOnly

[[ -d ./go/telemclient/RawModel ]] && rm -r ./go/telemclient/RawModel
$gen --modelFile ./dtdl/RawModel.json --outDir ./go/telemclient --lang go --clientOnly

[[ -d ./go/telemserver/RawModel ]] && rm -r ./go/telemserver/RawModel
$gen --modelFile ./dtdl/RawModel.json --outDir ./go/telemserver --lang go --serverOnly

[[ -d ./go/telemclient/CustomModel ]] && rm -r ./go/telemclient/CustomModel
$gen --modelFile ./dtdl/CustomModel.json --outDir ./go/telemclient --lang go --clientOnly

[[ -d ./go/telemserver/CustomModel ]] && rm -r ./go/telemserver/CustomModel
$gen --modelFile ./dtdl/CustomModel.json --outDir ./go/telemserver --lang go --serverOnly

[[ -d ./go/cmdclient/CounterCollection ]] && rm -r ./go/cmdclient/CounterCollection
$gen --modelFile ./dtdl/CounterCollection.json --outDir ./go/cmdclient --lang go --clientOnly

[[ -d ./go/cmdserver/CounterCollection ]] && rm -r ./go/cmdserver/CounterCollection
$gen --modelFile ./dtdl/CounterCollection.json --outDir ./go/cmdserver --lang go --serverOnly
