rm -rf ./StateStoreGen
../../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --things ../../../../eng/wot/statestore/StateStore.TM.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services --noProj --namespace GeneratedTypes --common GeneratedCommon --typeNamer ../../../../eng/wot/statestore/SchemaNames.json
