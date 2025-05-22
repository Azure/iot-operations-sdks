#!/bin/sh

gen=../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler

[[ -d ./SemanticDataClient ]] && rm -r ./SemanticDataClient
$gen --modelFile ./dtdl/SemanticDataModel.json --outDir ./SemanticDataClient --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --clientOnly

[[ -d ./SemanticDataServer ]] && rm -r ./SemanticDataServer
$gen --modelFile ./dtdl/SemanticDataModel.json --outDir ./SemanticDataServer --lang csharp --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol --serverOnly

UnitExtractor/bin/Debug/net9.0/UnitExtractor dtdl/SemanticDataModel.json ./SemanticDataHub/FirstModel.units.json
