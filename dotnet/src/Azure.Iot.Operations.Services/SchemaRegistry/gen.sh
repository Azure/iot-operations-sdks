rm -rf ./dtmi_ms_adr_SchemaRegistry__1
mkdir ./dtmi_ms_adr_SchemaRegistry__1
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/SchemaRegistry-1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry
cp -f /tmp/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry/dtmi_ms_adr_SchemaRegistry__1/*.cs dtmi_ms_adr_SchemaRegistry__1 -v
cp -f /tmp/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry -v
