#!/bin/sh

gen=../src/TypeTester/bin/Debug/net9.0/TypeTester

[[ -d PropertySeparate/DotNet ]] && rm -r PropertySeparate/DotNet
$gen PropertySeparate/JsonSchemas/Namespace PropertySeparate/DotNet C#

[[ -d PropertyTogether/DotNet ]] && rm -r PropertyTogether/DotNet
$gen PropertyTogether/JsonSchemas/Namespace PropertyTogether/DotNet C#

[[ -d PropertySeparate/Rust ]] && rm -r PropertySeparate/Rust
$gen PropertySeparate/JsonSchemas/Namespace PropertySeparate/Rust Rust

[[ -d PropertyTogether/Rust ]] && rm -r PropertyTogether/Rust
$gen PropertyTogether/JsonSchemas/Namespace PropertyTogether/Rust Rust

[[ -d CommandComplexSchemas/DotNet ]] && rm -r CommandComplexSchemas/DotNet
$gen CommandComplexSchemas/JsonSchemas/Namespace CommandComplexSchemas/DotNet C#

[[ -d CommandComplexSchemas/Rust ]] && rm -r CommandComplexSchemas/Rust
$gen CommandComplexSchemas/JsonSchemas/Namespace CommandComplexSchemas/Rust Rust

[[ -d CounterCollection/DotNet ]] && rm -r CounterCollection/DotNet
$gen CounterCollection/JsonSchemas/Namespace CounterCollection/DotNet C#

[[ -d CounterCollection/Rust ]] && rm -r CounterCollection/Rust
$gen CounterCollection/JsonSchemas/Namespace CounterCollection/Rust Rust

[[ -d TelemetryComplexSchemas/DotNet ]] && rm -r TelemetryComplexSchemas/DotNet
$gen TelemetryComplexSchemas/JsonSchemas/Namespace TelemetryComplexSchemas/DotNet C#

[[ -d TelemetryComplexSchemas/Rust ]] && rm -r TelemetryComplexSchemas/Rust
$gen TelemetryComplexSchemas/JsonSchemas/Namespace TelemetryComplexSchemas/Rust Rust
