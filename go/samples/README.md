# Go Samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.

This folder contains samples for the various Azure IoT Operations Go modules.

## Run a sample

> [!TIP]
> Update the `.env` file in the repository root directory to change the authentication method with MQTT broker.

1. Follow the [setup](/doc/setup.md) directions to prepare an Azure IoT Operations cluster for development.

1. Open a shell and navigate to the sample directory

1. Build the sample:

    ```bash
    go build .
    ```

1. Run the sample using the default [environment](/.env):

    ```bash
    source `git rev-parse --show-toplevel`/.env
    go run .
    ```

## Protocol samples

Protocol samples are located in the [protocol directory](./protocol). Each sample contains three directories:

| Directory | Contents | Description |
|-|-|-|
| `server` | Server side sample | The command executor in the case of RPC or the telemetry sender |
| `client` | Client side sample | The command invoker in the case of RPC or the telemetry receiver |
| `envoy` | Common protocol infrastructure | The common type definitions  and scaffolding that allow the client and server to communicate |

To run a sample, navigate to its directory (`go/samples/protocol/<sample>`) and start the server and client:

```bash
source `git rev-parse --show-toplevel`/.env
go run ./server
go run ./client
```

### Protocol Compiler

Some of these samples ([cloudevents](protocol/cloudevents) and [counter](protocol/counter)) use an envoy generated using the [Protocol Compiler](/codegen). To regenerate these envoys, build the Protocol compiler and run the `gen.sh` script in the corresponding `envoy` directory.

## Services samples

Services samples are located in the [services directory](./services). Each sample utilizes a client to interact with an Azure IoT Operations service.
To run one a sample, navigate to its directory (`go/samples/services/<sample>`) and run it via:

```bash
source `git rev-parse --show-toplevel`/.env
go run .
```
