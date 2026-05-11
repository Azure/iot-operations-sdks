#!/bin/sh
set -e
rm -rf ./src/schema_registry/schemaregistry_gen
dotnet run --project ../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --clientThings ../../eng/wot/schemaregistry/SchemaRegistry.TM.json --outDir src/schema_registry/schemaregistry_gen --lang rust \
 --namespace schema_registry --workingDir target/akri/SchemaRegistry --sdkPath ../ --noProj

rm -rf ./src/azure_device_registry/adr_base_gen
dotnet run --project ../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --clientThings ../../eng/wot/adr-base-service/AdrBaseServiceClientSide.TM.json --serverThings ../../eng/wot/adr-base-service/AdrBaseServiceServerSide.TM.json --outDir src/azure_device_registry/adr_base_gen --lang rust \
 --namespace adr_base_service --workingDir target/akri/AdrBaseService --sdkPath ../ --noProj

rm -rf ./src/azure_device_registry/device_discovery_gen
dotnet run --project ../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --clientThings ../../eng/wot/device-discovery-service/DeviceDiscoveryService.TM.json --outDir src/azure_device_registry/device_discovery_gen --lang rust \
 --namespace device_discovery_service --workingDir target/akri/DeviceDiscoveryService --sdkPath ../ --noProj

rm -rf ./src/edge_registry/edge_registry_gen
dotnet run --project ../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --clientThings ../../eng/wot/edge-registry/EdgeRegistry.TM.json ../../eng/wot/edge-registry/SchemaExtensions.TM.json ../../eng/wot/edge-registry/ThingDescriptionExtensions.TM.json --schemas ../../eng/wot/edge-registry/core-xregistry/*.schema.json ../../eng/wot/edge-registry/schema-extension/*.schema.json ../../eng/wot/edge-registry/thing-description-extension/*.schema.json --outDir ./src/edge_registry/edge_registry_gen --lang rust \
 --namespace edge_registry_client --workingDir target/akri/EdgeRegistry --noProj
 
cargo fmt
