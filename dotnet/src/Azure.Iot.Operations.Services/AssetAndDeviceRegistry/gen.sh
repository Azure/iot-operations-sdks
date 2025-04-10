rm -rf ./AdrBaseService
mkdir ./AdrBaseService
rm -rf ./AepTypeService
mkdir ./AepTypeService
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/aep-name-based-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/AdrBaseService/*.cs AdrBaseService -v
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry -v

../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/aep-type-based-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/AepTypeService/*.cs AepTypeService -v
cp -f /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.AssetAndDeviceRegistry -v
