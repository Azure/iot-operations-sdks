// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "context"

type (
	// Message represents a received message.
	Message struct {
		Topic   string
		Payload []byte
		PublishOptions

		// Ack will manually ack the message. All handled messages must be acked
		// (except for QoS 0 messages, in which case this is a no-op).
		Ack func()
	}

	// MessageHandler is a user-defined callback function used to handle
	// messages received on the subscribed topic.
	MessageHandler = func(context.Context, *Message)

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
		Error      error
	}

	// DisconnectEventHandler is a user-defined callback function used to
	// respond to disconnection notifications from the MQTT client.
	DisconnectEventHandler = func(*DisconnectEvent)

	// Ack contains values from PUBACK/SUBACK/UNSUBACK packets received from the
	// MQTT server.
	Ack struct {
		ReasonCode     byte
		ReasonString   string
		UserProperties map[string]string
	}
)
