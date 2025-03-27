#!/bin/sh
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/SchemaRegistry-1.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/schema_registry/schemaregistry_gen

../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/aep-name-based-operations.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/adr/adr_name_gen

../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler \
 --clientOnly --modelFile ../../eng/dtdl/aep-type-based-operations.json --sdkPath ../ --lang=rust --noProj \
 --outDir src/adr/adr_type_gen

cargo fmt
