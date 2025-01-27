# protocol

| [API Documentation](API.md) | [Samples](../samples/protocol) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=go%2Fprotocol)
|

## Overview

This module implements a protocol over MQTT which allows for structured data to
be sent and received between applications using two patterns:

-   RPC Command - Send requests, process them, and respond.
-   Telemetry - Send and receive telemetry messages.

## Simple RPC Request and Response

```go
package main

import (
	"context"
	"fmt"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type (
	Ping struct{ Message string }
	Pong struct{ Message string }
)

const (
	reqTopic = "mqtt/ping"
	resTopic = "mqtt/pong"
)

var (
	pingEncoding = protocol.JSON[Ping]{}
	pongEncoding = protocol.JSON[Pong]{}
)

func main() {
	// Note: Error handling omitted for simplicity.

	ctx := context.Background()
	app, _ := protocol.NewApplication()

	// Create a new session client for client and server (typically these would
	// be separate services; they're shown here together for simplicity).
	//
	// See the documentation of the mqtt module for more details.
	server := mqtt.NewSessionClient(mqtt.TCPConnection("localhost", 1883))
	client := mqtt.NewSessionClient(mqtt.TCPConnection("localhost", 1883))

	// Create a new executor to handle the requests.
	executor, _ := protocol.NewCommandExecutor(
		app,
		server,
		pingEncoding,
		pongEncoding,
		reqTopic,
		func(
			ctx context.Context,
			req *protocol.CommandRequest[Ping],
		) (*protocol.CommandResponse[Pong], error) {
			fmt.Printf("Ping received: %s\n", req.Payload.Message)
			return protocol.Respond(Pong{req.Payload.Message})
		},
	)
	defer executor.Close()

	// Create a new invoker to send the requests.
	invoker, _ := protocol.NewCommandInvoker(
		app,
		client,
		pingEncoding,
		pongEncoding,
		reqTopic,
		protocol.WithResponseTopic(func(string) string { return resTopic }),
	)
	defer invoker.Close()

	// Executors and invokers should be created before calling Start on the MQTT
	// client to ensure they can handle requests from any existing session.
	server.Start()
	defer server.Stop()
	client.Start()
	defer client.Stop()

	// Start listening to requests on the MQTT connection.
	executor.Start(ctx)
	invoker.Start(ctx)

	// Invoke the request.
	res, _ := invoker.Invoke(ctx, Ping{"Hello!"})
	fmt.Printf("Pong received: %s\n", res.Payload.Message)
}
```
