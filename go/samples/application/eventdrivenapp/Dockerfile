# Build application
FROM mcr.microsoft.com/devcontainers/go:1 AS build
WORKDIR /build
COPY . .
ENV CGO_ENABLED=0 GOOS=linux GOARCH=amd64 
RUN go build -C samples/application/eventdrivenapp

# Build runtime image
FROM mcr.microsoft.com/azurelinux/distroless/base:3.0
WORKDIR /
COPY --from=build /build/samples/application/eventdrivenapp/eventdrivenapp .
ENTRYPOINT ["./eventdrivenapp"]
