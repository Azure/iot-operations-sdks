#!/bin/sh
go run github.com/dave/courtney@v0.4.4 \
    github.com/Azure/iot-operations-sdks/go/internal/... \
    github.com/Azure/iot-operations-sdks/go/mqtt/... \
    github.com/Azure/iot-operations-sdks/go/protocol/... \
    github.com/Azure/iot-operations-sdks/go/services/... \
    github.com/Azure/iot-operations-sdks/go/test/integration/... \
    github.com/Azure/iot-operations-sdks/go/test/protocol/...

# Manually remove lines matching the test directories to avoid having to name
# every file in them *_test.go.
sed -i '/github.com\/Azure\/iot-operations-sdks\/go\/test/d' coverage.out
