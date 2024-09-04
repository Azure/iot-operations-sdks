module github.com/microsoft/mqtt-patterns/lib/go/mqtt

go 1.21

require (
	github.com/eclipse/paho.golang v0.21.0
	github.com/gorilla/websocket v1.5.3
	github.com/microsoft/mqtt-patterns/lib/go/protocol v0.0.0
	github.com/mochi-mqtt/server/v2 v2.6.4
	github.com/princjef/mageutil v1.0.0
	github.com/sosodev/duration v1.3.1
	github.com/stretchr/testify v1.9.0
	golang.org/x/crypto v0.26.0
)

require (
	github.com/VividCortex/ewma v1.2.0 // indirect
	github.com/cheggaaa/pb/v3 v3.1.5 // indirect
	github.com/davecgh/go-spew v1.1.1 // indirect
	github.com/fatih/color v1.16.0 // indirect
	github.com/mattn/go-colorable v0.1.13 // indirect
	github.com/mattn/go-isatty v0.0.20 // indirect
	github.com/mattn/go-runewidth v0.0.15 // indirect
	github.com/pmezard/go-difflib v1.0.0 // indirect
	github.com/rivo/uniseg v0.4.7 // indirect
	github.com/rs/xid v1.5.0 // indirect
	github.com/stretchr/objx v0.5.2 // indirect
	golang.org/x/sys v0.23.0 // indirect
	gopkg.in/yaml.v3 v3.0.1 // indirect
)

replace github.com/microsoft/mqtt-patterns/lib/go/protocol => ../protocol
