# Go Samples

This folder contains samples for the various Azure IoT Operations Go modules.

## Prerequisites

The samples in this repository assume a broker is running on `localhost`. The
[github.com/Azure/iot-operations-sdks/go/mqtt](../mqtt) module is intended for
use with the Azure IoT Operations MQTT Broker, but it is compatible with any
MQTTv5 broker.

Depending on the sample, the MQTT connection settings may be configured via code
or via [environment variables](../../doc/reference/connection-settings.md).

## Samples for [github.com/Azure/iot-operations-sdks/go/protocol](../protocol)

Each of these samples contains three directories:

-   `server` - This contains an example of the server side of the protocol - the
    command executor in the case of RPC or the telemetry sender.
-   `client` - This contains an example of the client side of the protocol - the
    command invoker in the case of RPC or the telemetry receiver.
-   `envoy` - This contains the common type definitions and scaffolding that
    allow the client and server to communicate.

To run one of the samples, navigate to its directory
(`go/samples/protocol/<sample>`) and run its server and/or client via:

```bash
go run ./server
go run ./client
```

### Protocol Compiler

Some of these samples ([cloudevents](protocol/cloudevents) and
[counter](protocol/counter)) use an envoy generated using the
[Protocol Compiler](../../codegen). To regenerate these envoys, build the
Protocol compiler and run the `gen.sh` script in the corresponding `envoy`
directory.

## Samples for [github.com/Azure/iot-operations-sdks/go/services](../services)

These contain samples of interactions with the Azure IOT Operations Services. To
run one of the samples, navigate to its directory
(`go/samples/services/<sample>`) and run it via:

```bash
go run .
```
