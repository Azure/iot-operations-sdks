# Build application
FROM golang:1 AS build
WORKDIR /build
COPY . .
RUN CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build .

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build /build/eventdrivenapp .
ENTRYPOINT ["./eventdrivenapp"]
