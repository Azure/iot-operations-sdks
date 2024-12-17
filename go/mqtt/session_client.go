// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"math"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
	"github.com/eclipse/paho.golang/paho"
	"github.com/eclipse/paho.golang/paho/session"
	"github.com/eclipse/paho.golang/paho/session/state"
)

type (
	// SessionClient implements an MQTT session client supporting MQTT v5 with
	// QoS 0 and QoS 1 support.
	SessionClient struct {
		// Used to ensure Start() is called only once and that user operations
		// are only started after Start() is called.
		sessionStarted atomic.Bool

		// Used to signal client shutdown for cleaning up background goroutines
		// and inflight operations. Only valid once started.
		shutdown *internal.Background

		// Tracker for the connection. Only valid once started.
		conn *internal.ConnectionTracker[*paho.Client]

		// A list of functions that listen for incoming messages.
		messageHandlers *internal.AppendableListWithRemoval[messageHandler]

		// A list of functions that are called in order to notify the user of
		// successful MQTT connections.
		connectEventHandlers *internal.AppendableListWithRemoval[ConnectEventHandler]

		// A list of functions that are called in order to notify the user of a
		// disconnection from the MQTT server.
		disconnectEventHandlers *internal.AppendableListWithRemoval[DisconnectEventHandler]

		// A list of functions that are called in goroutines to notify the user
		// of a session client termination due to a fatal error.
		fatalErrorHandlers *internal.AppendableListWithRemoval[func(error)]

		// Buffered channel containing the PUBLISH packets to be sent.
		outgoingPublishes chan *outgoingPublish

		// Paho's internal MQTT session tracker.
		session session.SessionManager

		connectionProvider ConnectionProvider
		options            SessionClientOptions

		log internal.Logger
	}
)

// NewSessionClient constructs a new session client with user options.
func NewSessionClient(
	connectionProvider ConnectionProvider,
	opts ...SessionClientOption,
) *SessionClient {
	// Default client options.
	client := &SessionClient{
		connectionProvider: connectionProvider,

		conn:                    internal.NewConnectionTracker[*paho.Client](),
		messageHandlers:         internal.NewAppendableListWithRemoval[messageHandler](),
		connectEventHandlers:    internal.NewAppendableListWithRemoval[ConnectEventHandler](),
		disconnectEventHandlers: internal.NewAppendableListWithRemoval[DisconnectEventHandler](),
		fatalErrorHandlers:      internal.NewAppendableListWithRemoval[func(error)](),

		outgoingPublishes: make(chan *outgoingPublish, maxPublishQueueSize),

		session: state.NewInMemory(),
	}

	client.options.Apply(opts)

	if client.options.ClientID == "" {
		client.options.ClientID = internal.RandomClientID()
	}

	if client.options.KeepAlive == 0 {
		client.options.KeepAlive = 60
	}

	if client.options.SessionExpiry == 0 {
		client.options.SessionExpiry = math.MaxUint32
	}

	if client.options.ReceiveMaximum == 0 {
		client.options.ReceiveMaximum = math.MaxUint16
	}

	if client.options.ConnectionRetry == nil {
		client.options.ConnectionRetry = &retry.ExponentialBackoff{
			Logger: client.options.Logger,
		}
	}

	client.log.Logger = log.Wrap(client.options.Logger)

	return client
}

// ID returns the MQTT client ID for this session client.
func (c *SessionClient) ID() string {
	return c.options.ClientID
}
