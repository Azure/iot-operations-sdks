TMPDIR=/tmp
codegen="../../../../../tools/codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen"
$codegen --modelFile dss.json --lang csharp --outDir $TMPDIR/Azure.Iot.Operations.Services.StateStore.Gen
rm -rf ./StateStoreGen
mkdir ./StateStoreGen
cp -f $TMPDIR/Azure.Iot.Operations.Services.StateStore.Gen/dtmi_ms_aio_mq_StateStore__1/*.cs ./StateStoreGen -v
cp -f $TMPDIR/Azure.Iot.Operations.Services.StateStore.Gen/PassthroughSerializer.cs ./StateStoreGen -v
rm -rf $TMPDIR/Azure.Iot.Operations.Services.StateStore.Gen -v
