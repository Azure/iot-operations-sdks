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

	// Puback contains values from PUBACK packets received from the MQTT server.
	// Note that there is no type for PUBREC or PUBCOMP packets because we don't
	// support QoS 2 publishes.
	Puback struct {
		ReasonCode     byte
		ReasonString   string
		UserProperties map[string]string
	}

	// Suback contains values from SUBACK packets recieved from the MQTT server.
	Suback struct {
		// NOTE: ReasonCode is a byte rather than a slice of bytes because we
		// don't support subscribing to mutiple topic filters in a single
		// subscribe operation.
		ReasonCode    byte
		ReasonString  string
		UserProprties map[string]string
	}

	// Unsuback contains values from UNSUBACK packets received from the MQTT
	// server.
	Unsuback struct {
		// NOTE: ReasonCode is a byte rather than a slice of bytes because we
		// don't support unsubscribing from mutiple topic filters in a single
		// unsubscribe operation.
		ReasonCode     byte
		ReasonString   string
		UserProperties map[string]string
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
