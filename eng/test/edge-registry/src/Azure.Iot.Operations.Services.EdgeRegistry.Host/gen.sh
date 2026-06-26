#!/bin/sh
set -e

# Generates the SERVER-side (executor) code for the Edge Registry xRegistry surface so the
# in-process test host can act as the service the SDK client talks to. The shipping SDK
# (dotnet/src/Azure.Iot.Operations.Services/EdgeRegistry) is intentionally client-only; this
# host owns the server half.
#
# Scoped to the core Thing Model (Group / Resource / Version) — the surface the integration
# tests exercise. To host an extension surface, add its TM to --serverThings and its schema
# glob to --schemas (mirrors the client gen.sh in the SDK):
#   --serverThings .../EdgeRegistry.TM.json .../SchemaExtensions.TM.json ...
#   --schemas '.../core-xregistry/*.schema.json' '.../schema-extension/*.schema.json' ...

rm -rf ./Generated

dotnet run --project ../../../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --serverThings ../../../../../eng/wot/edge-registry/EdgeRegistry.TM.json \
 --schemas '../../../../../eng/wot/edge-registry/core-xregistry/*.schema.json' \
 --outDir ./Azure.Iot.Operations.Services.EdgeRegistry.Host \
 --lang csharp \
 --namespace Generated \
 --common Generated.Common \
 --noProj

mv -f ./Azure.Iot.Operations.Services.EdgeRegistry.Host/** .
rm -rf ./Azure.Iot.Operations.Services.EdgeRegistry.Host
