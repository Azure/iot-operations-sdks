#!/bin/sh
../../../wot-codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net10.0/Azure.Iot.Operations.ProtocolCompiler \
    --things ../../../eng/test/schema-samples/Counter.TM.json --outDir ./envoy --sdkPath ../.. --lang rust \
    --namespace counter --workingDir ../../../target/akri/Counter
