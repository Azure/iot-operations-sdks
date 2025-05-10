#!/bin/sh
ROOT=$(git rev-parse --show-toplevel)

"$(find "$ROOT/codegen/src" -name Azure.Iot.Operations.ProtocolCompiler -type f)" \
	--modelFile "$ROOT/eng/dtdl/aep-name-based-operations.json" \
	--outDir "$ROOT/go/services/adr/internal" \
	--clientOnly --lang go

"$(find "$ROOT/codegen/src" -name Azure.Iot.Operations.ProtocolCompiler -type f)" \
	--modelFile "$ROOT/eng/dtdl/aep-type-based-operations.json" \
	--outDir "$ROOT/go/services/adr/internal" \
	--clientOnly --lang go