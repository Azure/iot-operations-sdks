rm -rf ./Generated
rm -rf ./schemas

dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/adr-base-service/AdrBaseService.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry --noProj --namespace Generated.AdrBaseService --common Generated.Common --typeNamer ../../../../eng/wot/adr-base-service/SchemaNames.json
dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/device-discovery-service/DeviceDiscoveryService.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry --noProj --namespace Generated.DeviceDiscoveryService --common Generated.Common --typeNamer ../../../../eng/wot/device-discovery-service/SchemaNames.json

mv -f ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry/** .
rm -rf ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry
