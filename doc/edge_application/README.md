# Edge application development

The Azure IoT Operations SDKs directly supports building edge applications in a number of ways:

1. Provides a MQTT session client enabling simple secure connectivity to MQTT broker including automatic online credential renewal and session resuming on reconnection to minimum message loss.

1. The state store client provides a clean interface to get, set and observe key-values.

1. The telemetry client for sending and receiving telemetry message.

1. The command client for invoking and executing commands using RPC over MQTT.

![alt text](images/edge-applications.png)

## Setup

Developing an edge application requires a Kubernetes cluster with Azure IoT Operations deployed. In additional, the MQTT broker can be configured to allow access from off cluster.

Follow the [setup instructions](../setup.md) steps to get your development environment ready.

## Creating your first applications

Once your development environment is setup, its time to create your first application. The first step is to choose your preferred language. Currently the SDKs are available in .NET, Go and Rust.

Refer to each language directory for instructions on setting up for that particular language:

* [.NET SDK](/dotnet)
* [Go SDK](/go)
* [Rust SDK](/rust)

## Testing

## Creating a container

Some languages (such as .NET) have built in container support, however all binaries can be deployed using a dockerfile. A Dockerfile can be created to support both the build and the deployed images for easier future deployments. [Alpine](https://hub.docker.com/_/alpine) provides some of the smallest container sizes, so is often the recommended image to use for your final image.


.NET:

Rust:

### Go

A build and deploy image pipeline in a single Dockerfile.

```dockerfile
## build
FROM golang:1-bullseye AS build
WORKDIR /work
COPY main.go .
COPY go.mod .
COPY go.sum .
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o .

## deploy
FROM alpine:3
WORKDIR /
COPY --from=build work/dapr-quickstart-go /dapr-quickstart-go
EXPOSE 6001
ENTRYPOINT ["/dapr-quickstart-go"]
```

## Deploying to the cluster
