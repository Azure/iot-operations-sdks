rm -rf ./StateStoreGen
mkdir ./StateStoreGen
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/statestore.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.StateStore
cp -f /tmp/Azure.Iot.Operations.Services.StateStore/dtmi_ms_aio_mq_StateStore__1/*.cs ./StateStoreGen -v
cp -f /tmp/Azure.Iot.Operations.Services.StateStore/PassthroughSerializer.cs ./StateStoreGen -v
rm -rf /tmp/Azure.Iot.Operations.Services.StateStore -v
