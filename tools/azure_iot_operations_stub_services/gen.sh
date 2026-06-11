#!/bin/sh
../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net10.0/Azure.Iot.Operations.ProtocolCompiler \
 --serverOnly --modelFile ../../eng/dtdl/SchemaRegistry-1.json --lang=rust --noProj \
 --outDir src/schema_registry/schema_registry_gen
 
cargo fmt
