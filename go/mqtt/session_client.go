// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"crypto/tls"
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

		connSettings *connectionSettings
		connRetry    retry.Policy

		log internal.Logger
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
		// Note the connectionTimeout would work with connRetry.
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
		conn:                    internal.NewConnectionTracker[PahoClient](),
		messageHandlers:         internal.NewAppendableListWithRemoval[messageHandler](),
		connectEventHandlers:    internal.NewAppendableListWithRemoval[ConnectEventHandler](),
		disconnectEventHandlers: internal.NewAppendableListWithRemoval[DisconnectEventHandler](),
		fatalErrorHandlers:      internal.NewAppendableListWithRemoval[func(error)](),

		// TODO: make this queue size configurable
		outgoingPublishes: make(chan *outgoingPublish, maxPublishQueueSize),

		session: state.NewInMemory(),

		connSettings: &connectionSettings{
			serverURL: serverURL,
			clientID:  internal.RandomClientID(),
			// If receiveMaximum is 0, we can't establish connection.
			receiveMaximum: defaultReceiveMaximum,
		},
	}
	client.pahoConstructor = client.defaultPahoConstructor

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

func (c *SessionClient) ID() string {
	return c.connSettings.clientID
}
