#!/bin/sh

genFromDtdl=../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler
genFromWot=../../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler

[[ -d ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry ]] && rm -r ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry
$genFromDtdl --modelFile ./eng/dtdl/adr-base-service.json --outDir ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromDtdl --modelFile ./eng/dtdl/device-discovery-service.json --outDir ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromDtdl --modelFile ./eng/dtdl/adr-base-service.json --outDir ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry/adr_base_gen --lang rust --sdkPath ../../../rust
$genFromDtdl --modelFile ./eng/dtdl/device-discovery-service.json --outDir ./eng/dtdl/FromDtdl/AssetAndDeviceRegistry/device_discovery_gen --lang rust --sdkPath ../../../rust

[[ -d ./eng/dtdl/FromWot/AssetAndDeviceRegistry ]] && rm -r ./eng/dtdl/FromWot/AssetAndDeviceRegistry
$genFromWot --things ./eng/dtdl/AdrBaseService.TM.json --outDir ./eng/dtdl/FromWot/AssetAndDeviceRegistry --lang csharp --namespace AdrBaseService --workingDir obj/akri/AdrBaseService --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromWot --things ./eng/dtdl/DeviceDiscoveryService.TM.json --outDir ./eng/dtdl/FromWot/AssetAndDeviceRegistry --lang csharp --namespace DeviceDiscoveryService --workingDir obj/akri/DeviceDiscoveryService --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromWot --things ./eng/dtdl/AdrBaseService.TM.json --outDir ./eng/dtdl/FromWot/AssetAndDeviceRegistry/adr_base_gen --lang rust --namespace adr_base_service --workingDir target/akri/AdrBaseService --sdkPath ../../../rust
$genFromWot --things ./eng/dtdl/DeviceDiscoveryService.TM.json --outDir ./eng/dtdl/FromWot/AssetAndDeviceRegistry/device_discovery_gen --lang rust --namespace device_discovery_service --workingDir target/akri/DeviceDiscoveryService --sdkPath ../../../rust

[[ -d ./eng/dtdl/FromDtdl/SchemaRegistry ]] && rm -r ./eng/dtdl/FromDtdl/SchemaRegistry
$genFromDtdl --modelFile ./eng/dtdl/SchemaRegistry-1.json --outDir ./eng/dtdl/FromDtdl/SchemaRegistry --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromDtdl --modelFile ./eng/dtdl/SchemaRegistry-1.json --outDir ./eng/dtdl/FromDtdl/SchemaRegistry/schemaregistry_gen --lang rust --sdkPath ../../../rust

[[ -d ./eng/dtdl/FromWot/SchemaRegistry ]] && rm -r ./eng/dtdl/FromWot/SchemaRegistry
$genFromWot --things ./eng/dtdl/SchemaRegistry.TM.json --outDir ./eng/dtdl/FromWot/SchemaRegistry --lang csharp --namespace SchemaRegistry --workingDir obj/akri/SchemaRegistry --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromWot --things ./eng/dtdl/SchemaRegistry.TM.json --outDir ./eng/dtdl/FromWot/SchemaRegistry/schemaregistry_gen --lang rust --namespace schema_registry --workingDir target/akri/SchemaRegistry --sdkPath ../../../rust

[[ -d ./eng/dtdl/FromDtdl/StateStore ]] && rm -r ./eng/dtdl/FromDtdl/StateStore
$genFromDtdl --modelFile ./eng/dtdl/statestore.json --outDir ./eng/dtdl/FromDtdl/StateStore --lang csharp --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromDtdl --modelFile ./eng/dtdl/statestore.json --outDir ./eng/dtdl/FromDtdl/StateStore/state_store_gen --lang rust --sdkPath ../../../rust

[[ -d ./eng/dtdl/FromWot/StateStore ]] && rm -r ./eng/dtdl/FromWot/StateStore
$genFromWot --things ./eng/dtdl/StateStore.TM.json --outDir ./eng/dtdl/FromWot/StateStore --lang csharp --namespace StateStore --workingDir obj/akri/StateStore --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
$genFromWot --things ./eng/dtdl/StateStore.TM.json --outDir ./eng/dtdl/FromWot/StateStore/state_store_gen --lang rust --namespace state_store --workingDir obj/akri/StateStore --sdkPath ../../../dotnet/src/Azure.Iot.Operations.Protocol
