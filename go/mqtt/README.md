# mqtt

| [API Documentation](API.md) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=go%2Fmqtt)
|

## Overview

This module provides an MQTTv5 client with automatic session, reconnection, and
retry logic. It is designed to allow you to focus on application logic without
needing to consider the underlying connection state.

This module is intended for use with the Azure IoT Operations MQTT Broker, but
it is compatible with any MQTTv5 broker.

## Simple Send and Receive

```go
package main

import (
	"context"
	"fmt"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
)

const (
	clientID = "aio_example_client"
	hostname = "localhost"
	port     = 1883
	topic    = "hello/mqtt"
)

func main() {
	ctx := context.Background()

	// Create a new session client with the above settings.
	client := mqtt.NewSessionClient(
		mqtt.TCPConnection(hostname, port),
		mqtt.WithClientID(clientID),
	)

	// Message handlers should be registered before calling Start unless using
	// mqtt.WithCleanStart(true), to handle messages from an existing session.
	done := client.RegisterMessageHandler(
		func(ctx context.Context, msg *mqtt.Message) {
			fmt.Printf("Received: %s\n", msg.Payload)
		},
	)
	defer done()

	// Note: Error handling omitted for simplicity. In addition to error return
	// values, client.RegisterFatalErrorHandler is recommended to handle any
	// unrecoverable errors encountered during connection attempts.

	client.Start()
	defer client.Stop()

	// Subscribe to the topic.
	client.Subscribe(ctx, topic, mqtt.WithQoS(1))
	defer client.Unsubscribe(ctx, topic)

	// Publish 10 messages, then exit.
	for i := range 10 {
		client.Publish(ctx, topic, []byte(fmt.Sprintf("Hello %d", i+1)))
		time.Sleep(time.Second)
	}
}
```
