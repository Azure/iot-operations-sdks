#!/bin/sh

dotnet build dotnet/CommandComplexSchemasSample
dotnet build dotnet/CommandVariantsSample
dotnet build dotnet/Counters
dotnet build dotnet/TelemetryComplexSchemasSample
dotnet build dotnet/TelemetryPrimitiveSchemasSample

cd rust/command_complex_schemas_gen
cargo build
cd ../..

cd rust/command_variants_gen
cargo build
cd ../..

cd rust/counters
cargo build
cd ../..

cd rust/telemetry_complex_schemas_gen
cargo build
cd ../..

cd rust/telemetry_primitive_schemas_gen
cargo build
cd ../..
