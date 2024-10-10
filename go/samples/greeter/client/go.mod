module github.com/Azure/iot-operations-sdks/go/samples/greeter/client

go 1.23.0

require (
	github.com/Azure/iot-operations-sdks/go/mqtt v0.1.1
	github.com/Azure/iot-operations-sdks/go/protocol v0.1.1
	github.com/Azure/iot-operations-sdks/go/samples/greeter/envoy v0.0.0
	github.com/lmittmann/tint v1.0.5
)

require (
	github.com/VividCortex/ewma v1.2.0 // indirect
	github.com/cheggaaa/pb/v3 v3.1.5 // indirect
	github.com/eclipse/paho.golang v0.21.0 // indirect
	github.com/fatih/color v1.16.0 // indirect
	github.com/google/uuid v1.6.0 // indirect
	github.com/gorilla/websocket v1.5.3 // indirect
	github.com/mattn/go-colorable v0.1.13 // indirect
	github.com/mattn/go-isatty v0.0.20 // indirect
	github.com/mattn/go-runewidth v0.0.15 // indirect
	github.com/princjef/mageutil v1.0.0 // indirect
	github.com/relvacode/iso8601 v1.4.0 // indirect
	github.com/rivo/uniseg v0.4.7 // indirect
	github.com/sosodev/duration v1.3.1 // indirect
	golang.org/x/crypto v0.26.0 // indirect
	golang.org/x/sys v0.23.0 // indirect
)

replace github.com/Azure/iot-operations-sdks/go/samples/greeter/envoy => ../envoy
