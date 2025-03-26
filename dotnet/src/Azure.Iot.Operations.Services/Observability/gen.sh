rm -rf ./AkriObservabilityServiceMetricsApis
mkdir ./AkriObservabilityServiceMetricsApis
rm -rf ./Common
mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/akri-observability-metrics-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.AkriObservabilityServiceMetricsApis
cp -f /tmp/Azure.Iot.Operations.Services.AkriObservabilityServiceMetricsApis/AkriObservabilityServiceMetricsApis/*.cs AkriObservabilityServiceMetricsApis -v
cp -f /tmp/Azure.Iot.Operations.Services.AkriObservabilityServiceMetricsApis/*.cs Common -v
rm -rf /tmp/Azure.Iot.Operations.Services.AkriObservabilityServiceMetricsApis -v
