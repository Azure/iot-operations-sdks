#!/bin/sh
../../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net10.0/Azure.Iot.Operations.ProtocolCompiler \
    --modelFile oven.json --outDir . --lang go
