#!/bin/sh

rm -r ./envoy
../../../codegen2/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
    --thingFiles ../../../eng/test/schema-samples/Counter.TM.json --outDir ./envoy --sdkPath ../.. --lang=rust --namespace Counter --workingDir target/counter;
