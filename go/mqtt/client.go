// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"time"

	"github.com/eclipse/paho.golang/paho"
)

type (
	// WillMessage is a representation of the LWT message that can
	// be sent with the Connect packet.
	WillMessage struct {
		Retain  bool
		QoS     byte
		Topic   string
		Payload []byte
	}

	// WillProperties is a struct of the properties
	// that can be set for a Will in a Connect packet.
	WillProperties struct {
		PayloadFormat     byte
		WillDelayInterval time.Duration
		MessageExpiry     time.Duration
		ContentType       string
		ResponseTopic     string
		CorrelationData   []byte
		User              map[string]string
	}

	// PahoClient is the interface for the underlying MQTTv5 client used by
	// ManagedClient, intended for future client swapping and testing purpose.
	// Currently, the Paho client serves as the core implementation.
	PahoClient interface {
		Connect(
			ctx context.Context,
			packet *paho.Connect,
		) (*paho.Connack, error)

		Disconnect(
			packet *paho.Disconnect,
		) error

		Subscribe(
			ctx context.Context,
			packet *paho.Subscribe,
		) (*paho.Suback, error)

		Unsubscribe(
			ctx context.Context,
			packet *paho.Unsubscribe,
		) (*paho.Unsuback, error)

		PublishWithOptions(
			ctx context.Context,
			packet *paho.Publish,
			options paho.PublishOptions,
		) (*paho.PublishResponse, error)

		AddOnPublishReceived(
			f func(paho.PublishReceived) (bool, error),
		) func()

		Ack(
			pb *paho.Publish,
		) error

		Authenticate(
			ctx context.Context,
			auth *paho.Auth,
		) (*paho.AuthResponse, error)
	}

	// PahoConstructor creates a PahoClient from a config.
	PahoConstructor = func(
		context.Context,
		*paho.ClientConfig,
	) (PahoClient, error)
)
