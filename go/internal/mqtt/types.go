// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "context"

type (
	// Message represents a received message. The client implementation must
	// support manual ack, since acks are managed by the protocol.
	Message struct {
		Topic   string
		Payload []byte
		PublishOptions
		Ack func() error
	}

	// MessageHandler is a user-defined callback function used to handle
	// messages received on the subscribed topic. Returns whether the handler
	// takes ownership of the message.
	MessageHandler = func(context.Context, *Message) bool

	// ConnectEvent contains the relevent metadata provided to the handler when
	// the MQTT client connects to the broker.
	ConnectEvent struct {
		ReasonCode byte
	}

	// ConnectEventHandler is a user-defined callback function used to respond
	// to connection notifications from the MQTT client.
	ConnectEventHandler = func(*ConnectEvent)

	// DisconnectEvent contains the relevent metadata provided to the handler
	// when the MQTT client disconnects from the broker.
	DisconnectEvent struct {
		ReasonCode *byte
	}

	// DisconnectEventHandler is a user-defined callback function used to
	// respond to disconnection notifications from the MQTT client.
	DisconnectEventHandler = func(*DisconnectEvent)
)
