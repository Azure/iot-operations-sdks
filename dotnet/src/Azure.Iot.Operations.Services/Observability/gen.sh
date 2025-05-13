#rm -rf ./AdrBaseService
#mkdir ./AdrBaseService
#rm -rf ./AepTypeService
#mkdir ./AepTypeService
#rm -rf ./Common
#mkdir ./Common
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler --modelFile ../../../../eng/dtdl/akri-observability-metrics-operations.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Observability
#cp -f /tmp/Azure.Iot.Operations.Services.Observability/AdrBaseService/*.cs AdrBaseService -v
#cp -f /tmp/Azure.Iot.Operations.Services.Observability/*.cs Common -v
#rm -rf /tmp/Azure.Iot.Operations.Services.Observability -v
