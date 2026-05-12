rm -rf ./Generated
dotnet run --project ../../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
    --clientThings ../../../../eng/wot/edge-registry/EdgeRegistry.TM.json ../../../../eng/wot/edge-registry/SchemaExtensions.TM.json ../../../../eng/wot/edge-registry/ThingDescriptionExtensions.TM.json \
    --schemas ../../../../eng/wot/edge-registry/core-xregistry/*.schema.json ../../../../eng/wot/edge-registry/schema-extension/*.schema.json ../../../../eng/wot/edge-registry/thing-description-extension/*.schema.json \
    --lang csharp --outDir ./Azure.Iot.Operations.Services.EdgeRegistry --noProj --namespace Generated --common Generated.Common 
mv -f ./Azure.Iot.Operations.Services.EdgeRegistry/** .
rm -rf ./Azure.Iot.Operations.Services.EdgeRegistry
 