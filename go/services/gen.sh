#!/bin/sh
set -e

ROOT=$(git rev-parse --show-toplevel)
CODE=$(find "$ROOT/codegen/src" -name Azure.Iot.Operations.ProtocolCompiler -type f)
DTDL=$ROOT/eng/dtdl
HERE=$(dirname "$0")

"$CODE" \
    --modelFile "$DTDL/SchemaRegistry-1.json" \
    --outDir "$HERE/schemaregistry/internal" \
    --clientOnly --lang go
