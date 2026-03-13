#!/bin/sh

conv=./codegen2/src/Dtdl2Wot/bin/Debug/net9.0/Dtdl2Wot

$conv ./eng/dtdl/adr-base-service.json ./eng/wot/adr-base-service
$conv ./eng/dtdl/device-discovery-service.json ./eng/wot/device-discovery-service
$conv ./eng/dtdl/SchemaRegistry-1.json ./eng/wot/schemaregistry
$conv ./eng/dtdl/statestore.json ./eng/wot/statestore
