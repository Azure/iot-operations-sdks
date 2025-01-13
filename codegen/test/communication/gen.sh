set -e

[[ -d ./dotnet/ProtocolCompiler.CommunicationTests/JsonComm ]] && rm -r ./dotnet/ProtocolCompiler.CommunicationTests/JsonComm
../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ./dtdl/JsonModel.json --outDir ./dotnet/ProtocolCompiler.CommunicationTests/JsonComm --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d ./dotnet/ProtocolCompiler.CommunicationTests/AvroComm ]] && rm -r ./dotnet/ProtocolCompiler.CommunicationTests/AvroComm
../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ./dtdl/AvroModel.json --outDir ./dotnet/ProtocolCompiler.CommunicationTests/AvroComm --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
