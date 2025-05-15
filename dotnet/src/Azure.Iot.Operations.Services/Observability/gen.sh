rm -rf ./AkriObservabilityService
mkdir ./AkriObservabilityService
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/akri-observability-metrics-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Observability
cp -f /tmp/Azure.Iot.Operations.Services.Observability/AkriObservabilityService/*.cs AkriObservabilityService -v
cp -f /tmp/Azure.Iot.Operations.Services.Observability/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.Observability -v
