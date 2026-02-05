#!/bin/sh

conv=../../src/Dtdl2Wot/bin/Debug/net9.0/Dtdl2Wot.exe

$conv ./eng/dtdl/adr-base-service.json ./eng/dtdl
$conv ./eng/dtdl/device-discovery-service.json ./eng/dtdl
$conv ./eng/dtdl/SchemaRegistry-1.json ./eng/dtdl
$conv ./eng/dtdl/statestore.json ./eng/dtdl
