#!/bin/sh
rm -r ./src/schema_registry/schemaregistry_gen
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/SchemaRegistry-1.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/schema_registry/schemaregistry_gen

rm -r ./src/azure_device_registry/adr_base_gen
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --modelFile ../../eng/dtdl/adr-base-service.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/azure_device_registry/adr_base_gen

rm -r ./src/azure_device_registry/device_discovery_gen
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/device-discovery-service.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/azure_device_registry/device_discovery_gen

cargo fmt
