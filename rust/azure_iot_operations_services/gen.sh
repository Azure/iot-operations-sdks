#!/bin/sh
rm -r ./src/schema_registry/schemaregistry_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientThings ../../eng/wot/schemaregistry/SchemaRegistry.TM.json --outDir src/schema_registry/schemaregistry_gen --lang rust \
 --namespace schema_registry --workingDir target/akri/SchemaRegistry --sdkPath ../ --noProj

rm -r ./src/azure_device_registry/adr_base_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientThings ../../eng/wot/adr-base-service/AdrBaseService.TM.json --outDir src/azure_device_registry/adr_base_gen --lang rust \
 --namespace adr_base_service --workingDir target/akri/AdrBaseService --sdkPath ../ --noProj

rm -r ./src/azure_device_registry/device_discovery_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientThings ../../eng/wot/device-discovery-service/DeviceDiscoveryService.TM.json --outDir src/azure_device_registry/device_discovery_gen --lang rust \
 --namespace device_discovery_service --workingDir target/akri/DeviceDiscoveryService --sdkPath ../ --noProj

cargo fmt
