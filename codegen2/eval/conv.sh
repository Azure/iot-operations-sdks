#!/bin/sh

conv=../src/Dtdl2Wot/bin/Debug/net9.0/Dtdl2Wot.exe

$conv ./dtdl/test/CommandComplexSchemas.json ./conv

$conv ./dtdl/test/CommandRaw.json ./conv

$conv ./dtdl/test/CommandVariants.json ./conv

$conv ./dtdl/test/PropertySeparate.json ./conv

$conv ./dtdl/test/PropertyTogether.json ./conv

$conv ./dtdl/test/TelemetryAndCommand.json ./conv

$conv ./dtdl/test/TelemetryComplexSchemas.json ./conv

$conv ./dtdl/test/TelemetryPrimitiveSchemas.json ./conv ./dtdl/test/resolver.json

$conv ./dtdl/test/TelemetryRawSeparate.json ./conv
