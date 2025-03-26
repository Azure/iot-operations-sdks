rm -rf ./AkriObservabilityServiceMetricsApis
mkdir ./AkriObservabilityServiceMetricsApis
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/akri-observability-metrics-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Observability
cp -f /tmp/Azure.Iot.Operations.Services.Observability/AkriObservabilityServiceMetricsApis/*.cs AkriObservabilityServiceMetricsApis -v
cp -f /tmp/Azure.Iot.Operations.Services.Observability/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.Observability -v
