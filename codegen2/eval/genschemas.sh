#!/bin/sh

gen=../src/SchemaTester/bin/Debug/net9.0/SchemaTester

[[ -d PropertySeparate/JsonSchemas ]] && rm -r PropertySeparate/JsonSchemas
$gen PropertySeparate PropertySeparate/JsonSchemas

[[ -d PropertyTogether/JsonSchemas ]] && rm -r PropertyTogether/JsonSchemas
$gen PropertyTogether PropertyTogether/JsonSchemas

[[ -d CommandComplexSchemas/JsonSchemas ]] && rm -r CommandComplexSchemas/JsonSchemas
$gen CommandComplexSchemas CommandComplexSchemas/JsonSchemas

[[ -d CounterCollection/JsonSchemas ]] && rm -r CounterCollection/JsonSchemas
$gen CounterCollection CounterCollection/JsonSchemas

[[ -d TelemetryComplexSchemas/JsonSchemas ]] && rm -r TelemetryComplexSchemas/JsonSchemas
$gen TelemetryComplexSchemas TelemetryComplexSchemas/JsonSchemas
