#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/ExternalSchemasOnlySample ]] && rm -r dotnet/ExternalSchemasOnlySample
$gen --schemas wot/ExternalSchemas/*.json --outDir dotnet/ExternalSchemasOnlySample --lang csharp --namespace ExternalSchemas --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/external_schemas_only_gen ]] && rm -r rust/external_schemas_only_gen
$gen --schemas wot/ExternalSchemas/*.json --outDir rust/external_schemas_only_gen --lang rust --namespace ExternalSchemas --sdkPath ../../rust
