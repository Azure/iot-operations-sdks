rm -rf ./SchemaRegistry
mkdir ./SchemaRegistry
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/SchemaRegistry-1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.SchemaRegistry
cp -f /tmp/Azure.Iot.Operations.Services.SchemaRegistry/SchemaRegistry/*.cs SchemaRegistry -v
cp -f /tmp/Azure.Iot.Operations.Services.SchemaRegistry/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.SchemaRegistry -v
