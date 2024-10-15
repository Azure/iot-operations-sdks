// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"crypto/tls"
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
		shutdown chan struct{}

		// Used internally to signal that the user has requested to stop the
		// client
		userStop          chan struct{}
		closeUserStopOnce sync.Once

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
		incomingPublishHandlers *internal.AppendableListWithRemoval[func(incomingPublish)]

		// A list of functions that are called in order to notify the user of
		// successful MQTT connections
		connectNotificationHandlers *internal.AppendableListWithRemoval[ConnectNotificationHandler]

		// A list of functions that are called in order to notify the user of a
		// disconnection from the MQTT server.
		disconnectNotificationHandlers *internal.AppendableListWithRemoval[DisconnectNotificationHandler]

		// A list of functions that are called in goroutines to notify the user
		// of a SessionClient termination due to a fatal error.
		fatalErrorHandlers *internal.AppendableListWithRemoval[func(error)]

		// Buffered channel containing the PUBLISH packets to be sent
		outgoingPublishes chan *outgoingPublish

		// Paho's internal MQTT session tracker
		session session.SessionManager

		// Paho client constructor (by default paho.NewClient + Conn)
		pahoConstructor PahoConstructor

		connSettings *connectionSettings
		connRetry    retry.Policy

		log logger
	}

	connectionSettings struct {
		clientID string
		// serverURL would be parsed into url.URL.
		serverURL string
		username  string
		password  []byte
		// Path to the password file. It would override password
		// if both are provided.
		passwordFile string

		// If keepAlive is 0,the Client is not obliged to send
		// MQTT Control Packets on any particular schedule.
		keepAlive time.Duration
		// If sessionExpiry is absent, its value 0 is used.
		sessionExpiry time.Duration
		// If receiveMaximum value is absent, its value defaults to 65,535.
		receiveMaximum uint16
		// If connectionTimeout is 0, connection will have no timeout.
		// Note the connectionTimeout would work with retrypolicy `connRetry`.
		connectionTimeout time.Duration
		userProperties    map[string]string

		// TLS transport protocol.
		useTLS bool
		// User can provide either a complete TLS configuration
		// or specify individual TLS parameters.
		// If both are provided, the individual parameters will take precedence.
		tlsConfig *tls.Config
		// Path to the client certificate file (PEM-encoded).
		certFile string
		// keyFilePassword would allow loading
		// an RFC 7468 PEM-encoded certificate
		// along with its password-protected private key,
		// similar to the .NET method CreateFromEncryptedPemFile.
		keyFile         string
		keyFilePassword string
		// Path to the certificate authority (CA) file (PEM-encoded).
		caFile string
		// TODO: check the revocation status of the CA.
		caRequireRevocationCheck bool

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
		shutdown: make(chan struct{}),
		userStop: make(chan struct{}),
		connUp:   make(chan struct{}),
		connDown: make(chan struct{}),

		incomingPublishHandlers:        internal.NewAppendableListWithRemoval[func(incomingPublish)](),
		connectNotificationHandlers:    internal.NewAppendableListWithRemoval[ConnectNotificationHandler](),
		disconnectNotificationHandlers: internal.NewAppendableListWithRemoval[DisconnectNotificationHandler](),
		fatalErrorHandlers:             internal.NewAppendableListWithRemoval[func(error)](),

		// TODO: make this queue size configurable
		outgoingPublishes: make(chan *outgoingPublish, math.MaxUint16),

		session: state.NewInMemory(),

		connSettings: &connectionSettings{
			serverURL: serverURL,
			clientID:  randomClientID(),
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
	if err := client.connSettings.validate(); err != nil {
		return nil, err
	}

	return client, nil
}

// NewSessionClientFromConnectionString constructs a new session client
// from an user-defined connection string.
func NewSessionClientFromConnectionString(
	connStr string,
	opts ...SessionClientOption,
) (*SessionClient, error) {
	connSettings := &connectionSettings{}
	if err := connSettings.fromConnectionString(connStr); err != nil {
		return nil, err
	}

	opts = append(opts, withConnSettings(connSettings))
	return NewSessionClient(connSettings.serverURL, opts...)
}

// NewSessionClientFromEnv constructs a new session client
// from user's environment variables.
func NewSessionClientFromEnv(
	opts ...SessionClientOption,
) (*SessionClient, error) {
	connSettings := &connectionSettings{}
	if err := connSettings.fromEnv(); err != nil {
		return nil, err
	}

	opts = append(opts, withConnSettings(connSettings))
	return NewSessionClient(connSettings.serverURL, opts...)
}

func (c *SessionClient) ClientID() string {
	return c.connSettings.clientID
}
