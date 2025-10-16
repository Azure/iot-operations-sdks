#!/bin/sh

gen=../src/EnvoyTester/bin/Debug/net9.0/EnvoyTester

$gen CommandVariants CommandVariants/DotNet CommandVariants C# ../../dotnet/src/Azure.Iot.Operations.Protocol

$gen CommandVariants CommandVariants/Rust CommandVariants Rust ../../rust

$gen CommandComplexSchemas CommandComplexSchemas/DotNet CommandComplexSchemas C# ../../dotnet/src/Azure.Iot.Operations.Protocol

$gen CommandComplexSchemas CommandComplexSchemas/Rust CommandComplexSchemas Rust ../../rust

$gen CounterCollection CounterCollection/DotNet Counters C# ../../dotnet/src/Azure.Iot.Operations.Protocol

$gen CounterCollection CounterCollection/Rust Counters Rust ../../rust

$gen TelemetryComplexSchemas TelemetryComplexSchemas/DotNet TelemetryComplexSchemas C# ../../dotnet/src/Azure.Iot.Operations.Protocol

$gen TelemetryComplexSchemas TelemetryComplexSchemas/Rust TelemetryComplexSchemas Rust ../../rust
