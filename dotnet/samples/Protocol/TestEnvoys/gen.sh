#!/bin/sh
# Counter, CustomTopicTokens, and Passthrough are generated from WoT Thing Models.
# Math (Protobuf) and Memmon (Avro) use payload formats that the WoT protocol compiler does not support,
# so their code is checked in and no longer regenerated.
set -e
CODEGEN=../../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net10.0/Azure.Iot.Operations.ProtocolCompiler
SDK=../../../src/Azure.Iot.Operations.Protocol

rm -rf ./Counter ./CustomTopicTokens ./Passthrough ./schemas

$CODEGEN --things ../../../../eng/test/schema-samples/Counter.TM.json --outDir . --sdkPath $SDK --namespace Counter --lang csharp --noProj --workingDir schemas/Counter
$CODEGEN --things ../../../../eng/test/schema-samples/CustomTopicTokens.TM.json --outDir . --sdkPath $SDK --namespace CustomTopicTokens --lang csharp --noProj --workingDir schemas/CustomTopicTokens
$CODEGEN --things Passthrough.TM.json --outDir . --sdkPath $SDK --namespace Passthrough --lang csharp --noProj --workingDir schemas/Passthrough
