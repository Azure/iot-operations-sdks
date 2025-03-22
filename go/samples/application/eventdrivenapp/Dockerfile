# Build application
FROM golang:1 AS build
WORKDIR /build
COPY . .
ENV CGO_ENABLED=0 GOOS=linux GOARCH=amd64 
RUN go build .

# Build runtime image
FROM alpine:3
WORKDIR /
COPY --from=build /build/eventdrivenapp .
ENTRYPOINT ["./eventdrivenapp"]
