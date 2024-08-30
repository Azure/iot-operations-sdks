TMPDIR=/tmp
codegen="../../../../../tools/codegen/src/Akri.Dtdl.Codegen/bin/Debug/net8.0/Akri.Dtdl.Codegen"
$codegen --modelFile SchemaRegistry-1.json --lang csharp --outDir $TMPDIR/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
cp -f $TMPDIR/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry/dtmi_ms_adr_SchemaRegistry__1/*.cs dtmi_ms_adr_SchemaRegistry__1 -v
rm -rf $TMPDIR/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry -v