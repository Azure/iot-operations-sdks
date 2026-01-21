#!/bin/sh
rm -r ./src/schema_registry/schemaregistry_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --thingFiles ../../eng/dtdl/SchemaRegistry.TM.json --outDir src/schema_registry/schemaregistry_gen --lang rust \
 --namespace SchemaRegistry --workingDir target/akri/SchemaRegistry --sdkPath ../ --noProj --clientOnly

rm -r ./src/azure_device_registry/adr_base_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --thingFiles ../../eng/dtdl/AdrBaseService.TM.json --outDir src/azure_device_registry/adr_base_gen --lang rust \
 --namespace AdrBaseService --workingDir target/akri/AdrBaseService --sdkPath ../ --noProj

rm -r ./src/azure_device_registry/device_discovery_gen
../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --thingFiles ../../eng/dtdl/DeviceDiscoveryService.TM.json --outDir src/azure_device_registry/device_discovery_gen --lang rust \
 --namespace DeviceDiscoveryService --workingDir target/akri/DeviceDiscoveryService --sdkPath ../ --noProj --clientOnly

cargo fmt
