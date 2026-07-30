#!/bin/sh
set -e

# Generates the SERVER-side (executor) code for the Edge Registry xRegistry surface so the
# in-process test host can act as the service the SDK client talks to. The shipping SDK
# (dotnet/src/Azure.Iot.Operations.Services/EdgeRegistry) is intentionally client-only; this
# host owns the server half.
#
# Covers the core Thing Model (Group / Resource / Version) plus the Schema, Thing Description, and
# Thing Model extension surfaces — the same set of Thing Models the SDK client is generated from.

rm -rf ./Generated
rm -rf ./schemas

dotnet run --project ../../../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ \
 --serverThings ../../../../../eng/wot/edge-registry/EdgeRegistry.TM.json ../../../../../eng/wot/edge-registry/SchemaExtensions.TM.json ../../../../../eng/wot/edge-registry/ThingDescriptionExtensions.TM.json ../../../../../eng/wot/edge-registry/ThingModelExtensions.TM.json \
 --schemas '../../../../../eng/wot/edge-registry/core-xregistry/*.schema.json' '../../../../../eng/wot/edge-registry/schema-extension/*.schema.json' '../../../../../eng/wot/edge-registry/thing-description-extension/*.schema.json' '../../../../../eng/wot/edge-registry/thing-model-extension/*.schema.json' \
 --outDir ./Azure.Iot.Operations.Services.EdgeRegistry.Host \
 --lang csharp \
 --namespace Generated \
 --common Generated.Common \
 --noProj

mv -f ./Azure.Iot.Operations.Services.EdgeRegistry.Host/** .
rm -rf ./Azure.Iot.Operations.Services.EdgeRegistry.Host
