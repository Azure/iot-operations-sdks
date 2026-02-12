rm -rf ./Generated
dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/statestore/StateStore.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.StateStore --noProj --namespace Generated --common Generated.Common --typeNamer ../../../../eng/wot/statestore/SchemaNames.json
mv -f ./Azure.Iot.Operations.Services.StateStore/** .
rm -rf ./Azure.Iot.Operations.Services.StateStore
