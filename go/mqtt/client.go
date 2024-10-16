// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"time"

	"github.com/eclipse/paho.golang/paho"
)

type (
	// AuthOptions are the resolved options for enhanced authentication.
	AuthOptions struct {
		AuthMethod string
		AuthData   []byte
		// Path to the auth data file.
		// If AuthData and SatAuthFile are provided both,
		// the SatAuthFile will take precedence.
		SatAuthFile string
		// User-defined function to refresh AuthData, such as SAT token.
		AuthDataProvider AuthDataProvider
		// If user provides AuthDataProvider, we start reauthentication
		// periodically in the background with the AuthInterval.
		AuthInterval time.Duration
		// Auther is an interface for user to implement autheticate logic.
		// type Auther interface {
		// 		// Authenticate will be called when an AUTH packet is received.
		// 		Authenticate(*paho.Auth) *paho.Auth
		// 		// Authenticated will be called when CONNACK is received.
		// 		Authenticated()
		// }
		AuthHandler paho.Auther
	}

	// AuthOption represents a single authentication option.
	AuthOption interface{ authenticate(*AuthOptions) }

	// AuthDataProvider is a user-defined function used to
	// provide new AuthData for refreshing authentication.
	AuthDataProvider func(context.Context) []byte

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

		Publish(
			ctx context.Context,
			packet *paho.Publish,
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
)
