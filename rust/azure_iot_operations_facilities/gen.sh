#!/bin/sh
../../codegen2/src/Azure.Iot.Operations.UnitTabulator/bin/Debug/net9.0/Azure.Iot.Operations.UnitTabulator --outDir ./src/unit_converter --lang rust --kind Conversion
../../codegen2/src/Azure.Iot.Operations.UnitTabulator/bin/Debug/net9.0/Azure.Iot.Operations.UnitTabulator --outDir ./src/unit_selector --lang rust --kind Selection

cargo fmt
