rm -rf ./DiscoveredAssetResources
mkdir ./DiscoveredAssetResources
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile discovered_resources_commands_v1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Akri
cp -f /tmp/Azure.Iot.Operations.Services.Akri/DiscoveredAssetResources/*.cs DiscoveredAssetResources -v
cp -f /tmp/Azure.Iot.Operations.Services.Akri/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.Akri -v
