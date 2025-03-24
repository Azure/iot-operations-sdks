#!/bin/sh
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/SchemaRegistry-1.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/schema_registry/schemaregistry_gen

../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler \
 --serverOnly --modelFile ../../eng/dtdl/akri-observability-metrics-operations.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/observability/observability_gen

cargo fmt
