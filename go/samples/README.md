# GO Samples

This folder contains samples for GO based on a DTDL code generation tool. A MQTT
broker is needed to run them.

## Prerequisites

### Generate the Test Envoys

```sh
./codegen/build.sh

cd go/samples/protocol/counter/envoy
./gen.sh
```

## How to run

Each sample has 3 projects, the `envoy` with the generated code, the `client`
and the `server`.

### Counter

```bash
# from go/samples/protocol/counter/server
MQTT_HOST_NAME=localhost MQTT_USE_TLS=false MQTT_TCP_PORT=1883 MQTT_CLIENT_ID=CounterServer-go go run .

# from go/samples/protocol/counter/client
MQTT_HOST_NAME=localhost MQTT_USE_TLS=false MQTT_TCP_PORT=1883 COUNTER_SERVER_ID=CounterServer-go go run .
```
