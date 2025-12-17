#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/ExternalSchemasSample ]] && rm -r dotnet/ExternalSchemasSample
$gen --thingFiles wot/ExternalSchemas.TD.json --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasSample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_gen ]] && rm -r rust/external_schemas_gen
$gen --thingFiles wot/ExternalSchemas.TD.json --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_gen --lang rust --namespace ExternalSchemas --sdkPath ../../rust --srcSubdir src
