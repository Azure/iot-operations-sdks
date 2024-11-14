#!/bin/sh
../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Release/net8.0/Azure.Iot.Operations.ProtocolCompiler \
    --modelFile ../../../eng/test/schema-samples/counter.json --outDir ./envoy --lang rust --sdkPath ../..
