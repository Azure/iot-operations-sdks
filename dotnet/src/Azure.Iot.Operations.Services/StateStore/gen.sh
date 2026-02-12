dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/statestore/StateStore.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services --noProj --namespace StateStore.Generated --common StateStore.Generated.Common --typeNamer ../../../../eng/wot/statestore/SchemaNames.json
mv -f ./Azure.Iot.Operations.Services.StateStore/** .
rm -rf ./Azure.Iot.Operations.Services.StateStore
