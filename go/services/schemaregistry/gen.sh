#!/bin/sh
ROOT=$(git rev-parse --show-toplevel)
"$ROOT/codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler" \
    --modelFile "$ROOT/eng/dtdl/SchemaRegistry-1.json" \
    --outDir "$ROOT/go/services/schemaregistry" \
    --clientOnly --lang go
