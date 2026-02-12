rm -rf ./Generated
dotnet run --project ../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/ --things ../../../../eng/wot/schemaregistry/SchemaRegistry.TM.json --lang csharp --outDir ./Azure.Iot.Operations.Services.SchemaRegistry --noProj --namespace Generated --common Generated.Common --typeNamer ../../../../eng/wot/schemaregistry/SchemaNames.json
mv -f ./Azure.Iot.Operations.Services.SchemaRegistry/** .
rm -rf ./Azure.Iot.Operations.Services.SchemaRegistry
