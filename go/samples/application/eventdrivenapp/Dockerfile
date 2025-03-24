# Build application
FROM golang:1 AS build
WORKDIR /build
COPY . .
ENV CGO_ENABLED=0 GOOS=linux GOARCH=amd64 
RUN go build -C samples/application/eventdrivenapp

# Build runtime image
FROM debian:bookworm-slim
WORKDIR /
COPY --from=build /build/samples/application/eventdrivenapp/eventdrivenapp .
ENTRYPOINT ["./eventdrivenapp"]
