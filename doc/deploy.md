# Deploy an edge application

The following instruction outline how to create a container image for your application, push it to the cluster and deploy to Kubernetes.

## Creating a container

Some languages have built in container support, however all binaries can be deployed using a Dockerfile. A Dockerfile can be created to support building the project and the creating the deployable container for repeatable container creation by using [multi-stage builds](https://docs.docker.com/build/building/multi-stage/).

[Alpine Docker](https://hub.docker.com/_/alpine) provides some of the smallest container sizes, so is often the recommended image to use for the runtime image.

> [!NOTE]
> The Dockerfile examples below are for reference only, and should be adapted for your particular situation.

### .NET

Refer to [Containerize a .NET app](https://learn.microsoft.com/dotnet/core/docker/build-container) for details on building and creating a container from a .NET 8 project.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /App

# Build application
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
```

### Rust

```dockerfile
FROM rust:1.82 AS build
WORKDIR /work

# Build application
COPY . .
RUN cargo install –path .

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build work/rust-application .
ENTRYPOINT ["/rust-application"]
```

### Go

```dockerfile
FROM golang:1.23 AS build
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

## Uploading to a container registry

Import the image from docker into the k3d cluster using the [k3d image import](https://k3d.io/v5.1.0/usage/commands/k3d_image_import/) command:

```bash
k3d image import <image-name>
```

## Deploying to the cluster

The following yaml defines a `ServiceAccount` and `Deployment` for the edge application container. 

It contains the following information:

1. A Service Account Token for authentication with MQTT broker
1. A CA trust bundle for validating the MQTT broker certificate
1. The container image built earlier

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
            #imagePullPolicy: Never  # Set this to Never will allow pulling the imported image from Docker
            
            volumeMounts:
            - name: mqtt-client-token
              mountPath: /var/run/secrets/tokens
            - name: aio-ca-trust-bundle
              mountPath: /var/run/certs/aio-ca-cert
    ```

1. Apply the yaml to the cluster:

    ```bash
    kubectl apply -f app.yml
    ```
