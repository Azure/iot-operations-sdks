#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

# Clean previous output (no error if it doesn't exist yet)
Remove-Item -Recurse -Force ./Generated -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force ./schemas -ErrorAction SilentlyContinue

$tmpDir = './Azure.Iot.Operations.Services.EdgeRegistry'

dotnet run --project ../../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/ `
 --clientThings ../../../../eng/wot/edge-registry/EdgeRegistry.TM.json ../../../../eng/wot/edge-registry/SchemaExtensions.TM.json ../../../../eng/wot/edge-registry/ThingDescriptionExtensions.TM.json ../../../../eng/wot/edge-registry/ThingModelExtensions.TM.json `
 --schemas '../../../../eng/wot/edge-registry/core-xregistry/*.schema.json' '../../../../eng/wot/edge-registry/schema-extension/*.schema.json' '../../../../eng/wot/edge-registry/thing-description-extension/*.schema.json' '../../../../eng/wot/edge-registry/thing-model-extension/*.schema.json' `
 --outDir $tmpDir `
 --lang csharp `
 --namespace Generated `
 --common Generated.Common `
 --noProj

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Flatten generated output up into this folder, replacing any stale items, then drop the temp dir
Get-ChildItem $tmpDir -Force | ForEach-Object {
    $dest = Join-Path . $_.Name
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    Move-Item $_.FullName $dest -Force
}
Remove-Item $tmpDir -Recurse -Force
