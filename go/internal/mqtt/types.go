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

	// SubscribeOptions are the resolved subscribe options.
	SubscribeOptions struct {
		NoLocal        bool
		QoS            byte
		Retain         bool
		RetainHandling byte
		UserProperties map[string]string
	}

	// SubscribeOption represents a single subscribe option.
	SubscribeOption interface{ subscribe(*SubscribeOptions) }

	// UnsubscribeOptions are the resolve unsubscribe options.
	UnsubscribeOptions struct {
		UserProperties map[string]string
	}

	// UnsubscribeOption represents a single unsubscribe option.
	UnsubscribeOption interface{ unsubscribe(*UnsubscribeOptions) }

	// PublishOptions are the resolved publish options.
	PublishOptions struct {
		ContentType     string
		CorrelationData []byte
		MessageExpiry   uint32
		PayloadFormat   byte
		QoS             byte
		ResponseTopic   string
		Retain          bool
		UserProperties  map[string]string
	}

	// PublishOption represents a single publish option.
	PublishOption interface{ publish(*PublishOptions) }
)
