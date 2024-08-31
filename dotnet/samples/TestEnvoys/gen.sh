set -e

gen=../../../tools/codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen 
sdkPath=../../../lib/dotnet/src/Azure.Iot.Operations.Protocol

$gen --modelFile counter.json --outDir . --sdkPath $sdkPath
$gen --modelFile math.json --outDir . --sdkPath $sdkPath
$gen --modelFile memmon.json --outDir . --sdkPath $sdkPath
$gen --modelFile passthrough.json --outDir . --sdkPath $sdkPath

