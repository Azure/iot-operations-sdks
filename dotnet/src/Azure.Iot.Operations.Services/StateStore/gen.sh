../../../../codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen --modelFile dss.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.StateStore.Gen
rm -rf ./StateStoreGen
mkdir ./StateStoreGen
cp -f /tmp/StateStore/dtmi_ms_aio_mq_StateStore__1/*.cs ./StateStoreGen -v
cp -f /tmp/StateStore/PassthroughSerializer.cs ./StateStoreGen -v
rm -rf /tmp/StateStore -v