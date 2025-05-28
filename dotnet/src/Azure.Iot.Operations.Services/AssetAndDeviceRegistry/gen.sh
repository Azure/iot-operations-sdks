rm -rf ./AdrBaseService
mkdir ./AdrBaseService
rm -rf ./DeviceDiscoveryService
mkdir ./DeviceDiscoveryService
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/adr-base-service.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/AdrBaseService/*.cs AdrBaseService -v
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry -v

../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/device-discovery-service.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/DeviceDiscoveryService/*.cs DeviceDiscoveryService -v
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry -v
