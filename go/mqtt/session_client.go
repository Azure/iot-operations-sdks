// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"math"
	"sync"
	"sync/atomic"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
	"github.com/eclipse/paho.golang/paho/session"
	"github.com/eclipse/paho.golang/paho/session/state"
)

type (
	// SessionClient implements an MQTT Session client supporting MQTT v5 with
	// QoS 0 and QoS 1 support.
	SessionClient struct {
		// Used to ensure Connect() is called only once and that user operations
		// are only started after Connect() is called
		sessionStarted atomic.Bool

		// Used to internally to signal client shutdown for cleaning up
		// background goroutines and inflight operations
		shutdown <-chan struct{}
		stop     func()

		// RWMutex to protect pahoClient, connUp, connDown, and connCount
		pahoClientMu sync.RWMutex
		// Instance of a Paho client. Underlying implmentation may be an
		// instance of a real paho.Client or it may be a stub client used for
		// testing
		pahoClient PahoClient
		// Channel that is closed when the connection is up (i.e., a new Paho
		// client instance is created and connected to the server with a
		// successful CONNACK), used to notify goroutines that are waiting on a
		// connection to be re-establised
		connUp chan struct{}
		// Channel that is closed when the the connection is down. Used to
		// notify goroutines that expect the connection to go down that the
		// manageConnection() goroutine has detected the disconnection and is
		// attempting to start a new connection
		connDown chan struct{}
		// The number of successful connections that have ocurred on the session
		// client, up to and including the current Paho client instance
		connCount uint64

		// A list of functions that listen for incoming publishes
		incomingPublishHandlers *internal.AppendableListWithRemoval[func(incomingPublish) bool]

		// A list of functions that are called in order to notify the user of
		// successful MQTT connections
		connectEventHandlers *internal.AppendableListWithRemoval[ConnectEventHandler]

		// A list of functions that are called in order to notify the user of a
		// disconnection from the MQTT server.
		disconnectEventHandlers *internal.AppendableListWithRemoval[DisconnectEventHandler]

		// A list of functions that are called in goroutines to notify the user
		// of a SessionClient termination due to a fatal error.
		fatalErrorHandlers *internal.AppendableListWithRemoval[func(error)]

		// Buffered channel containing the PUBLISH packets to be sent
		outgoingPublishes chan *outgoingPublish

		// Paho's internal MQTT session tracker
		session session.SessionManager

		// Paho client constructor (by default paho.NewClient + Conn)
		pahoConstructor PahoConstructor

		config    *connectionConfig
		connRetry retry.Policy

		log internal.Logger
	}

	connectionConfig struct {
		connectionProvider ConnectionProvider

		clientID string

		userNameProvider UserNameProvider
		passwordProvider PasswordProvider

		// If keepAlive is 0,the Client is not obliged to send
		// MQTT Control Packets on any particular schedule.
		keepAlive time.Duration
		// If sessionExpiry is absent, its value 0 is used.
		sessionExpiry time.Duration
		// If receiveMaximum value is absent, its value defaults to 65,535.
		receiveMaximum uint16
		// If connectionTimeout is 0, connection will have no timeout.
		// Note the connectionTimeout would work with connRetry.
		connectionTimeout time.Duration
		userProperties    map[string]string

		// Last Will and Testament (LWT) option.
		willMessage    *WillMessage
		willProperties *WillProperties
	}
)

// NewSessionClient constructs a new session client with user options.
func NewSessionClient(
	serverURL string,
	opts ...SessionClientOption,
) (*SessionClient, error) {
	// Default client options.
	client := &SessionClient{
		connUp:   make(chan struct{}),
		connDown: make(chan struct{}),

		incomingPublishHandlers: internal.NewAppendableListWithRemoval[func(incomingPublish) bool](),
		connectEventHandlers:    internal.NewAppendableListWithRemoval[ConnectEventHandler](),
		disconnectEventHandlers: internal.NewAppendableListWithRemoval[DisconnectEventHandler](),
		fatalErrorHandlers:      internal.NewAppendableListWithRemoval[func(error)](),

		// TODO: make this queue size configurable
		outgoingPublishes: make(chan *outgoingPublish, math.MaxUint16),

		session: state.NewInMemory(),

		config: &connectionConfig{
			serverURL: serverURL,
			clientID:  internal.RandomClientID(),
			// If receiveMaximum is 0, we can't establish connection.
			receiveMaximum: defaultReceiveMaximum,
		},
	}
	client.pahoConstructor = client.defaultPahoConstructor

	// Immediately close connDown to maintain the invariant that connDown is
	// closed iff the session client is disconnected.
	close(client.connDown)

	// User client settings.
	for _, opt := range opts {
		opt(client)
	}

	// Do this after options since we need the user-configured logger for the
	// default retry.
	if client.connRetry == nil {
		client.connRetry = &retry.ExponentialBackoff{Logger: client.log.Wrapped}
	}

	// Validate connection settings.
	if err := client.config.validate(); err != nil {
		return nil, err
	}

	return client, nil
}

// NewSessionClientFromConnectionString constructs a new session client
// from an user-defined connection string. Note that values from the
// connection string take priority over any functional options.
func NewSessionClientFromConnectionString(
	connStr string,
	opts ...SessionClientOption,
) (*SessionClient, error) {
	config := &connectionConfig{}
	if err := config.fromConnectionString(connStr); err != nil {
		return nil, err
	}

	opts = append(opts, withConnectionConfig(config))
	return NewSessionClient(config.serverURL, opts...)
}

// NewSessionClientFromEnv constructs a new session client
// from user's environment variables. Note that values from environment
// variables take priorty over any functional options.
func NewSessionClientFromEnv(
	opts ...SessionClientOption,
) (*SessionClient, error) {
	config := &connectionConfig{}
	if err := config.fromEnv(); err != nil {
		return nil, err
	}

	opts = append(opts, withConnectionConfig(config))
	return NewSessionClient(config.serverURL, opts...)
}

func (c *SessionClient) ID() string {
	return c.config.clientID
}
