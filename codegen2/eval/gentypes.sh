#!/bin/sh

gen=../src/TypeTester/bin/Debug/net9.0/TypeTester

[[ -d PropertySeparate/DotNet ]] && rm -r PropertySeparate/DotNet
$gen PropertySeparate/JsonSchemas/Namespace PropertySeparate/DotNet PropertySeparate C#

[[ -d PropertyTogether/DotNet ]] && rm -r PropertyTogether/DotNet
$gen PropertyTogether/JsonSchemas/Namespace PropertyTogether/DotNet PropertyTogether C#

[[ -d PropertySeparate/Rust ]] && rm -r PropertySeparate/Rust
$gen PropertySeparate/JsonSchemas/Namespace PropertySeparate/Rust PropertySeparate Rust

[[ -d PropertyTogether/Rust ]] && rm -r PropertyTogether/Rust
$gen PropertyTogether/JsonSchemas/Namespace PropertyTogether/Rust PropertyTogether Rust

[[ -d CommandVariants/DotNet ]] && rm -r CommandVariants/DotNet
$gen CommandVariants/JsonSchemas/Namespace CommandVariants/DotNet CommandVariants C#

[[ -d CommandVariants/Rust ]] && rm -r CommandVariants/Rust
$gen CommandVariants/JsonSchemas/Namespace CommandVariants/Rust CommandVariants Rust

[[ -d CommandComplexSchemas/DotNet ]] && rm -r CommandComplexSchemas/DotNet
$gen CommandComplexSchemas/JsonSchemas/Namespace CommandComplexSchemas/DotNet CommandComplexSchemas C#

[[ -d CommandComplexSchemas/Rust ]] && rm -r CommandComplexSchemas/Rust
$gen CommandComplexSchemas/JsonSchemas/Namespace CommandComplexSchemas/Rust CommandComplexSchemas Rust

[[ -d CounterCollection/DotNet ]] && rm -r CounterCollection/DotNet
$gen CounterCollection/JsonSchemas/Namespace CounterCollection/DotNet Counters C#

[[ -d CounterCollection/Rust ]] && rm -r CounterCollection/Rust
$gen CounterCollection/JsonSchemas/Namespace CounterCollection/Rust Counters Rust

[[ -d TelemetryComplexSchemas/DotNet ]] && rm -r TelemetryComplexSchemas/DotNet
$gen TelemetryComplexSchemas/JsonSchemas/Namespace TelemetryComplexSchemas/DotNet TelemetryComplexSchemas C#

[[ -d TelemetryComplexSchemas/Rust ]] && rm -r TelemetryComplexSchemas/Rust
$gen TelemetryComplexSchemas/JsonSchemas/Namespace TelemetryComplexSchemas/Rust TelemetryComplexSchemas Rust
