# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License

[CmdletBinding()]
param(
    [string]$InputRoot = (Join-Path $PSScriptRoot "..\..\UA-Nodeset"),
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\..\smd\models"),
    [switch]$Integrate,
    [switch]$InheritVars,
    [switch]$IncludeTDs,
    [switch]$IncludeNonPublished
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is required, but 'dotnet' was not found on PATH."
}

if (-not (Test-Path -LiteralPath $InputRoot -PathType Container)) {
    throw "The NodeSet input directory does not exist: $InputRoot"
}

$resolvedInputRoot = (Resolve-Path -LiteralPath $InputRoot).Path
$resolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$outputParent = Split-Path -Parent $resolvedOutputDir
if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) {
    throw "The output parent directory does not exist: $outputParent"
}

$projectPath = Join-Path $PSScriptRoot "src\Azure.Iot.Operations.Opc2Wot\Azure.Iot.Operations.Opc2Wot.csproj"
$excludedRelativePaths = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        "DemoModel\DemoModel.NodeSet2.xml",
        "LaserSystems\LaserSystem-Example.NodeSet2.xml",
        "TestModel\TestModel.NodeSet2.xml"
    ),
    [System.StringComparer]::OrdinalIgnoreCase)
$nodeSetFiles = Get-ChildItem -LiteralPath $resolvedInputRoot -Recurse -File -Filter "*.NodeSet2.xml" |
    Where-Object {
        $IncludeNonPublished -or
        -not $excludedRelativePaths.Contains([System.IO.Path]::GetRelativePath($resolvedInputRoot, $_.FullName))
    } |
    Sort-Object FullName

if ($nodeSetFiles.Count -eq 0) {
    throw "No *.NodeSet2.xml files were found below: $resolvedInputRoot"
}

$arguments = @(
    "run",
    "--project", $projectPath,
    "--configuration", "Debug",
    "--",
    "--nodeSets"
)
$arguments += $nodeSetFiles.FullName
$arguments += @("--outDir", $resolvedOutputDir)

if ($Integrate) {
    $arguments += "--integrate"
}
if ($InheritVars) {
    $arguments += "--inheritVars"
}
if ($IncludeTDs) {
    $arguments += "--includeTDs"
}

Write-Host "Generating WoT Thing Models from '$resolvedInputRoot' into '$resolvedOutputDir'."
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "OPC UA Thing Model generation failed with exit code $LASTEXITCODE."
}
