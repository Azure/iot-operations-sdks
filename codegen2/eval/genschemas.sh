#!/bin/sh

gen=../src/SchemaTester/bin/Debug/net9.0/SchemaTester

[[ -d PropertySeparate/JsonSchemas ]] && rm -r PropertySeparate/JsonSchemas
$gen PropertySeparate PropertySeparate/JsonSchemas PropertySeparate

[[ -d PropertyTogether/JsonSchemas ]] && rm -r PropertyTogether/JsonSchemas
$gen PropertyTogether PropertyTogether/JsonSchemas PropertyTogether

[[ -d CommandVariants/JsonSchemas ]] && rm -r CommandVariants/JsonSchemas
$gen CommandVariants CommandVariants/JsonSchemas CommandVariants

[[ -d CommandComplexSchemas/JsonSchemas ]] && rm -r CommandComplexSchemas/JsonSchemas
$gen CommandComplexSchemas CommandComplexSchemas/JsonSchemas CommandComplexSchemas

[[ -d CounterCollection/JsonSchemas ]] && rm -r CounterCollection/JsonSchemas
$gen CounterCollection CounterCollection/JsonSchemas Counters

[[ -d TelemetryComplexSchemas/JsonSchemas ]] && rm -r TelemetryComplexSchemas/JsonSchemas
$gen TelemetryComplexSchemas TelemetryComplexSchemas/JsonSchemas TelemetryComplexSchemas
