#!/bin/sh
ROOT=$(git rev-parse --show-toplevel)
CODE=$(find "$ROOT/codegen/src" -name Azure.Iot.Operations.ProtocolCompiler -type f)
DTDL=$ROOT/eng/dtdl
HERE=$(dirname "$0")

"$CODE" \
	--modelFile "$DTDL/device-name-based-operations.json" \
	--outDir "$HERE/adr/internal" \
	--clientOnly --lang go

"$CODE" \
	--modelFile "$DTDL/aep-type-based-operations.json" \
	--outDir "$HERE/adr/internal" \
	--clientOnly --lang go

"$CODE" \
    --modelFile "$DTDL/SchemaRegistry-1.json" \
    --outDir "$HERE/schemaregistry/internal" \
    --clientOnly --lang go
