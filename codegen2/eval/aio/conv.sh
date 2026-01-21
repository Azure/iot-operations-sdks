#!/bin/sh

conv=./codegen2/src/Dtdl2Wot/bin/Debug/net9.0/Dtdl2Wot

$conv ./eng/dtdl/adr-base-service.json ./eng/dtdl
$conv ./eng/dtdl/device-discovery-service.json ./eng/dtdl
$conv ./eng/dtdl/SchemaRegistry-1.json ./eng/dtdl
$conv ./eng/dtdl/statestore.json ./eng/dtdl
