// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"math"
	"sync/atomic"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
	"github.com/eclipse/paho.golang/paho/session"
	"github.com/eclipse/paho.golang/paho/session/state"
)

type (
	// SessionClient implements an MQTT Session client supporting MQTT v5 with
	// QoS 0 and QoS 1 support.
	SessionClient struct {
		// Used to ensure Start() is called only once and that user operations
		// are only started after Start() is called.
		sessionStarted atomic.Bool

		// Used to internally to signal client shutdown for cleaning up
		// background goroutines and inflight operations
		shutdown *internal.Background

		// Tracker for the connection. Only valid once started.
		conn *internal.ConnectionTracker[PahoClient]

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

		// Paho client constructor (by default paho.NewClient + Conn).
		pahoConstructor PahoConstructor

		config    *connectionConfig
		connRetry retry.Policy

		log internal.Logger
	}

	connectionConfig struct {
		connectionProvider ConnectionProvider
		authProvider       auth.Provider

		clientID string

		userNameProvider UserNameProvider
		passwordProvider PasswordProvider

		firstConnectionCleanStart bool
		keepAlive                 uint16
		sessionExpiryInterval     uint32
		receiveMaximum            uint16
		userProperties            map[string]string

		// If connectionTimeout is 0, connection will have no timeout.
		//
		// NOTE: this timeout applies to a single connection attempt.
		// Configuring a timeout accross multiple attempts can be done through
		// the retry policy.
		//
		// TODO: this is currently treated as the timeout for a single
		// connection attempt. Once discussion on this occurs, ensure this is
		// aligned with the other session client implementations and update the
		// note above if this has changed.
		connectionTimeout time.Duration
	}
)

// NewSessionClient constructs a new session client with user options.
func NewSessionClient(
	connectionProvider ConnectionProvider,
	opts ...SessionClientOption,
) (*SessionClient, error) {
	// Default client options.
	client := &SessionClient{
		conn:                    internal.NewConnectionTracker[PahoClient](),
		messageHandlers:         internal.NewAppendableListWithRemoval[messageHandler](),
		connectEventHandlers:    internal.NewAppendableListWithRemoval[ConnectEventHandler](),
		disconnectEventHandlers: internal.NewAppendableListWithRemoval[DisconnectEventHandler](),
		fatalErrorHandlers:      internal.NewAppendableListWithRemoval[func(error)](),

		outgoingPublishes: make(chan *outgoingPublish, maxPublishQueueSize),

		session: state.NewInMemory(),

		config: &connectionConfig{
			connectionProvider:        connectionProvider,
			userNameProvider:          defaultUserName,
			passwordProvider:          defaultPassword,
			clientID:                  internal.RandomClientID(),
			firstConnectionCleanStart: true,
			keepAlive:                 60,
			sessionExpiryInterval:     math.MaxUint32,
			receiveMaximum:            math.MaxUint16,
		},
	}
	client.pahoConstructor = client.defaultPahoConstructor

	for _, opt := range opts {
		opt(client)
	}

	// Do this after options since we need the user-configured logger for the
	// default retry.
	if client.connRetry == nil {
		client.connRetry = &retry.ExponentialBackoff{Logger: client.log.Wrapped}
	}

	return client, nil
}

// NewSessionClientFromConnectionString constructs a new session client from a
// user-defined connection string. Note that values from the connection string
// take priority over any functional options.
func NewSessionClientFromConnectionString(
	connStr string,
	opts ...SessionClientOption,
) (*SessionClient, error) {
	config, err := configFromConnectionString(connStr)
	if err != nil {
		return nil, err
	}

	opts = append(opts, withConnectionConfig(config))
	return NewSessionClient(config.connectionProvider, opts...)
}

// NewSessionClientFromEnv constructs a new session client
// from user's environment variables. Note that values from environment
// variables take priorty over any functional options.
func NewSessionClientFromEnv(
	opts ...SessionClientOption,
) (*SessionClient, error) {
	config, err := configFromEnv()
	if err != nil {
		return nil, err
	}

	opts = append(opts, withConnectionConfig(config))
	return NewSessionClient(config.connectionProvider, opts...)
}

func (c *SessionClient) ID() string {
	return c.config.clientID
}
