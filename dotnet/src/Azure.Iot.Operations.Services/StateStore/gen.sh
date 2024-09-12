../../../../../tools/codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile dss.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.StateStore.Gen
rm -rf ./StateStoreGen
mkdir ./StateStoreGen
cp -f /tmp/Azure.Iot.Operations.Services.StateStore.Gen/dtmi_ms_aio_mq_StateStore__1/*.cs ./StateStoreGen -v
cp -f /tmp/Azure.Iot.Operations.Services.StateStore.Gen/PassthroughSerializer.cs ./StateStoreGen -v
rm -rf /tmp/Azure.Iot.Operations.Services.StateStore.Gen -v