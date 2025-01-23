#!/bin/sh
../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler \
    --modelFile ../../../eng/test/schema-samples/complex.json --outDir ./complex_envoy --sdkPath ../.. --lang=rust;
