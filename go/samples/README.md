# GO Samples

This folder contains samples for GO based on a DTDL code generation tool. A MQTT
broker is needed to run them.

## Prerequisites

### Generate the Test Envoys

```bash
dotnet build codegen/src/Akri.Dtdl.Codegen/Akri.Dtdl.Codegen.csproj

pushd go/samples/counter/envoy
sh ./gen.sh
popd
```

## How to run

Each sample has 3 projects, the `envoy` with the generated code, the `client`
and the `server`.

### Counter

```bash
# from go/samples/counter/server
MQTT_HOST_NAME=localhost MQTT_USE_TLS=false MQTT_TCP_PORT=1883 MQTT_CLIENT_ID=CounterServer-go go run server.go

# from go/samples/counter/client
MQTT_HOST_NAME=localhost MQTT_USE_TLS=false MQTT_TCP_PORT=1883 COUNTER_SERVER_ID=CounterServer-go go run client.go
```
