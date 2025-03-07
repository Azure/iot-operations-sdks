# Go Samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.
>
> To learn more about securing your edge solution, refer to [Security best practices for IoT solutions](https://learn.microsoft.com/azure/iot/iot-overview-security).

This folder contains samples for the various Azure IoT Operations Go modules.

## Prerequisites

The samples in this repository assume a broker is running on `localhost`.

Depending on the sample, the MQTT connection settings may be configured via code or via [environment variables](/doc/reference/connection-settings.md).

## Protocol samples

Protocol samples can be found in the [Go protocol directory](../protocol). Each of these samples contains three directories:

- `server` - This contains an example of the server side of the protocol - the command executor in the case of RPC or the telemetry sender.
- `client` - This contains an example of the client side of the protocol - the command invoker in the case of RPC or the telemetry receiver.
- `envoy` - This contains the common type definitions and scaffolding that allow the client and server to communicate.

To run one of the samples, navigate to its directory (`go/samples/protocol/<sample>`) and run its server and/or client via:

```bash
go run ./server
go run ./client
```

### Protocol Compiler

Some of these samples ([cloudevents](protocol/cloudevents) and [counter](protocol/counter)) use an envoy generated using the [Protocol Compiler](../../codegen). To regenerate these envoys, build the Protocol compiler and run the `gen.sh` script in the corresponding `envoy` directory.

## Services samples

Services samples can be found in the [Go services directory](../services). These samples utilize a client to interact with various Azure IoT Operations services.

To run one a sample, navigate to its directory (`go/samples/services/<sample>`) and run it via:

```bash
go run .
```
