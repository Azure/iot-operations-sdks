#!/bin/sh
ROOT=$(git rev-parse --show-toplevel)
"$(find "$ROOT/codegen/src" -name Azure.Iot.Operations.ProtocolCompiler -type f)" \
    --modelFile "$ROOT/eng/dtdl/SchemaRegistry-1.json" \
    --outDir "$ROOT/go/services/schemaregistry" \
    --clientOnly --lang go
