rm -rf ./Generated

dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/adr-base-service/AdrBaseService.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry --noProj --namespace Generated --common Generated.Common --typeNamer ../../../../eng/wot/adr-base-service/SchemaNames.json
dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/device-discovery-service/DeviceDiscoveryService.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry --noProj --namespace Generated --common Generated.Common --typeNamer ../../../../eng/wot/device-discovery-service/SchemaNames.json

mv -f ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry/** .
rm -rf ./Azure.Iot.Operations.Services.AssetAndDeviceRegistry
