set -e

cd CommandVariantsSample/command_variants_gen
cargo build
cd ../..

cd CommandComplexSchemasSample/command_complex_schemas_gen
cargo build
cd ../..

cd CommandRawSample/command_raw_gen
cargo build
cd ../..

cd TelemetryAndCommandSample/telemetry_and_command_gen
cargo build
cd ../..

cd TelemetryAndCommandSampleClientOnly/telemetry_and_command_gen
cargo build
cd ../..

cd TelemetryAndCommandSampleServerOnly/telemetry_and_command_gen
cargo build
cd ../..

cd TelemetryComplexSchemasSample/telemetry_complex_schemas_gen
cargo build
cd ../..

cd TelemetryPrimitiveSchemasSample/telemetry_primitive_schemas_gen
cargo build
cd ../..

cd TelemetryRawSingleSample/telemetry_raw_single_gen
cargo build
cd ../..

cd TelemetryRawSeparateSample/telemetry_raw_separate_gen
cargo build
cd ../..

cd TelemetryAndCommandNestedRaw/telemetry_and_command_gen
cargo build
cd ../..
