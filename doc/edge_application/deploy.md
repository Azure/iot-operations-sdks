# Deploy an edge application

The following instruction outline how to create a container image for your application, push it to the a registry, and deploy to the cluster.

* [Container types](#container-types)
* [.NET Dockerfile](#net-dockerfile)
* [Rust Dockerfile](#rust-dockerfile)
* [Go Dockerfile](#go-dockerfile)
* [Build the container image](#build-the-container-image)
* [Import the container image](#import-the-container-image)
* [Deploying to the cluster](#deploying-to-the-cluster)

## Container types

Some languages have built in container support, however all binaries can be containerized using a Dockerfile. A Dockerfile can be created to support both building the project and the creating the deployable container for repeatable container creation by using [multi-stage builds](https://docs.docker.com/build/building/multi-stage/).

> [!WARNING]
> The Dockerfile examples below are for reference only, and should be adapted for your particular situation.

## .NET Dockerfile

> [!NOTE]
> 
> Refer to [Containerize a .NET app](https://learn.microsoft.com/dotnet/core/docker/build-container) for details on building and creating a container directly from a .NET project.

This definition uses the [Official .NET SDK image](https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md) to build the application, and then copies the resulting package into the [.NET runtime image](https://hub.docker.com/_/alpine).


```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Build application
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
```

## Rust Dockerfile

This definition uses the [Official Rust image](https://hub.docker.com/_/rust) to build the release application, and then copies the resulting binary (called "rust-application" in the example below) into the [Alpine image](https://hub.docker.com/_/alpine).

```dockerfile
FROM rust:1 AS build
WORKDIR /work

# Build application
COPY . .
RUN cargo build

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build work/rust-application .
ENTRYPOINT ["/rust-application"]
```

## Go Dockerfile

This definition uses the [Official Golang image](https://hub.docker.com/_/golang) to build the application, and then copies the resulting binary (called "go-application" in the example below) into the [Alpine image](https://hub.docker.com/_/alpine).

```dockerfile
FROM golang:1 AS build
WORKDIR /work

# Build application
COPY . .
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o .

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build work/go-application .
ENTRYPOINT ["/go-application"]
```

## Build the container image

1. Create a file called `Dockerfile` in your application directory, and define the container *(using above as a reference)*.

1. Build the container image, substituting "sample" with your own tag name as needed:

    ```bash
    docker build --tag sample .
    ```

1. The resulting binary will be available 

    ```bash
    docker image ls sample
    ```

    ```output
    REPOSITORY   TAG       IMAGE ID       CREATED          SIZE
    sample       latest    7b76808165ed   10 minutes ago   73.9MB
    ```

## Import the container image

Instead of uploading the container image to a container registry, you can choose to import the image directly into the k3d cluster using the [k3d image import](https://k3d.io/v5.1.0/usage/commands/k3d_image_import/) command. This avoids the need to use a container registry, and is ideal for developing locally.

```bash
k3d image import <image-name>
```

> [!TIP]
> If using the k3d import method described here, then make sure the `imagePullPolicy` in the container definition is set to `Never`, otherwise the cluster will attempt to download the image.

## Deploying to the cluster

The following yaml can be used as a reference for deploying your application to the cluster.

It contains the following information:

| Type | Name | Description |
|-|-|-|
| ServiceAccount | `sdk-application` | Used for generating the SAT for authentication to the broker |
| Deployment | `sdk-application` | The edge application deployment definition |
| Volume | `mqtt-client-token` | The SAT for mounting into the counter |
| Volume | `aio-ca-trust-bundle` | The broker trust-bundle for validating the server |
| Container | `sdk-application` | The definition of the container, including the container image and the mount locations for the SAT and broker trust-bundle |
| env | `MQTT_*`, `AIO_*` | The environment variables used to configure the connection to the MQTT broker. Refer to [MQTT broker access](/doc/setup.md#mqtt-broker-access) for details on settings these values to match the development environment |

> [!TIP]
> Setting the `imagePullPolicy` to `Never`, allows the cached image to be used even when the version is `latest`.

1. Create a file called `app.yaml` containing the following

    ```yaml
    apiVersion: v1
    kind: ServiceAccount
    metadata:
      name: sdk-application
      namespace: azure-iot-operations

    ---
    apiVersion: apps/v1
    kind: Deployment
    metadata:
      name: sdk-application
      namespace: azure-iot-operations
    spec:
      replicas: 1
      selector:
        matchLabels:
          app: sdk-application
      template:
        metadata:
          labels:
            app: sdk-application
        spec:
          serviceAccountName: sdk-application

          volumes:
          - name: mqtt-client-token
            projected:
              sources:
              - serviceAccountToken:
                  path: mqtt-client-token
                  audience: aio-internal
                  expirationSeconds: 86400
          - name: aio-ca-trust-bundle
            configMap:
              name: azure-iot-operations-aio-ca-trust-bundle

          containers:
          - name: sdk-application
            image: <image-name>
            imagePullPolicy: Never # Set to Never to use the imported image
            
            volumeMounts:
            - name: mqtt-client-token
              mountPath: /var/run/secrets/tokens
            - name: aio-ca-trust-bundle
              mountPath: /var/run/certs/aio-ca-cert

          env:
            - name: MQTT_CLIENT_ID
              value: <my-client-id>
            - name: AIO_BROKER_HOSTNAME
              value: aio-broker
            - name: AIO_BROKER_TCP_PORT
              value: 18883
            - name: AIO_MQTT_USE_TLS
              value: true
            - name: AIO_TLS_CA_FILE
              value: var/run/certs/aio-ca/ca.crt
            - name: AIO_SAT_FILE
              value: /var/run/secrets/tokens/mqtt-client-token
    ```

1. Apply the yaml to the cluster:

    ```bash
    kubectl apply -f app.yml
    ```
