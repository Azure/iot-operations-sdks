#!/bin/sh

gen=../src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler.exe

[[ -d dotnet/TelemetryAndCommandSample ]] && rm -r dotnet/TelemetryAndCommandSample
$gen --things conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSample --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_and_command_gen ]] && rm -r rust/telemetry_and_command_gen
$gen --things conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen --lang rust --namespace telemetry_and_command --sdkPath ../../rust

[[ -d dotnet/TelemetryAndCommandSampleClientOnly ]] && rm -r dotnet/TelemetryAndCommandSampleClientOnly
$gen --clientThings conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSampleClientOnly --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_and_command_gen_client_only ]] && rm -r rust/telemetry_and_command_gen_client_only
$gen --clientThings conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen_client_only --lang rust --namespace telemetry_and_command --sdkPath ../../rust

[[ -d dotnet/TelemetryAndCommandSampleServerOnly ]] && rm -r dotnet/TelemetryAndCommandSampleServerOnly
$gen --serverThings conv/TelemetryAndCommand.TM.json --outDir dotnet/TelemetryAndCommandSampleServerOnly --lang csharp --namespace TelemetryAndCommand --sdkPath ../../dotnet/src/Azure.Iot.Operations.Protocol

[[ -d rust/telemetry_and_command_gen_server_only ]] && rm -r rust/telemetry_and_command_gen_server_only
$gen --serverThings conv/TelemetryAndCommand.TM.json --outDir rust/telemetry_and_command_gen_server_only --lang rust --namespace telemetry_and_command --sdkPath ../../rust
