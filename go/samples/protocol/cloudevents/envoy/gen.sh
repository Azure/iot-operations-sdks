#!/bin/sh
../../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net9.0/Azure.Iot.Operations.ProtocolCompiler \
    --modelFile oven.json --outDir . --lang go
