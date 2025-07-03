#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler 

[[ -d ./dotnet/ProtocolCompiler.Demo/Counters ]] && rm -r ./dotnet/ProtocolCompiler.Demo/Counters
$gen --modelFile ./dtdl/CounterCollection.json --outDir ./dotnet/ProtocolCompiler.Demo/Counters --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol
