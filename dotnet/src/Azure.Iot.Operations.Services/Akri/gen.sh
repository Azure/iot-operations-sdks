rm -rf ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
mkdir ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile discovered_resources_commands_v1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Akri.Akri
cp -f /tmp/Azure.Iot.Operations.Services.Akri.Akri/dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1/*.cs dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1 -v
cp -f /tmp/Azure.Iot.Operations.Services.Akri.Akri/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.Akri.Akri -v
# end