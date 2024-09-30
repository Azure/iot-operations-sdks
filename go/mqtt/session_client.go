package mqtt

import (
	"crypto/tls"
	"fmt"
	"log/slog"
	"sync"
	"sync/atomic"
	"time"

	"github.com/eclipse/paho.golang/paho/session"
	"github.com/eclipse/paho.golang/paho/session/state"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
)

type (
	// SessionClient implements an MQTT Session client
	// supporting MQTT v5 with QoS 0 and QoS 1.
	// TODO: Add support for QoS 2.
	// TODO: make sure the initialization is valid.
	SessionClient struct {
		// Used to ensure that the SessionClient does not leak goroutines
		wg sync.WaitGroup

		// Used to ensure Connect() is called only once and that user
		// operations are only started after Connect() is called
		sessionStarted atomic.Bool

		// Used to internally to signal client shutdown for cleaning up background goroutines.
		shutdown chan struct{}

		// RWMutex to protect pahoClient, connUp, connDown, and connCount
		pahoClientMu sync.RWMutex
		// Instance of a Paho client. Underlying implmentation may be an instance of a real paho.Client or it may be a stub client used for testing.
		pahoClient PahoClient
		// Channel that is closed when the connection is up (i.e., a new Paho client instance is created and connected to the server with a successful CONNACK),
		// used to notify goroutines that are waiting on a connection to be re-establised.
		connUp chan struct{}
		// Channel that is closed when the the connection is down (i.e., when a goroutine sees an error from the current Paho client instance and expects a disconnection to occur),
		// used to notify goroutines that the connection management goroutine has detected a disconnection and is attempting to start a new connection.
		connDown chan struct{}
		// The number of successful connections that have ocurred on the session client, up to and including the current Paho client instance.
		connCount uint64

		// Mutex used to protect publishHandlers and publishHandlerTracker
		incomingPublishHandlerMu sync.Mutex
		// A slice of functions that listen for incoming publishes
		incomingPublishHandlers []func(incomingPublish)
		// A slice of unique IDs corresponding to the functions in incomingPublishHandlers, used to track handlers for removal.
		incomingPublishHandlerIDs []uint64

		// Buffered channel containing the PUBLISH packets to be sent
		outgoingPublishes chan *outgoingPublish

		// Paho's internal MQTT session tracker
		session session.SessionManager

		connSettings *connectionSettings
		connRetry    retrypolicy.RetryPolicy

		// The user-defined function would be called
		// when auto reauthentication returns an error.
		authErrHandler func(error)

		logger *slog.Logger

		// If debugMode is disabled, only error() will be printed.
		// If debugMode is enabled, the prettier logger provides
		// a more detailed client workflow, including info() and debug().
		debugMode bool
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

		cleanStart bool
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

		// Enhanced Authentication.
		authOptions *AuthOptions

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
	client := &SessionClient{}

	// Default client options.
	client.initialize()

	// Only required client setting.
	client.connSettings.serverURL = serverURL

	// User client settings.
	for _, opt := range opts {
		opt(client)
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
) (*SessionClient, error) {
	connSettings := &connectionSettings{}
	if err := connSettings.fromConnectionString(connStr); err != nil {
		return nil, err
	}

	client, err := NewSessionClient(
		connSettings.serverURL,
		withConnSettings(connSettings),
	)
	if err != nil {
		return nil, err
	}
	return client, nil
}

// NewSessionClientFromEnv constructs a new session client
// from user's environment variables.
func NewSessionClientFromEnv() (*SessionClient, error) {
	connSettings := &connectionSettings{}
	if err := connSettings.fromEnv(); err != nil {
		return nil, err
	}

	client, err := NewSessionClient(
		connSettings.serverURL,
		withConnSettings(connSettings),
	)
	if err != nil {
		return nil, err
	}
	return client, nil
}

func (c *SessionClient) ClientID() string {
	return c.connSettings.clientID
}

// initialize sets all default configurations
// to ensure the SessionClient is properly initialized.
func (c *SessionClient) initialize() {
	c.connRetry = retrypolicy.NewExponentialBackoffRetryPolicy()
	c.connSettings = &connectionSettings{
		clientID: randomClientID(),
		// If receiveMaximum is 0, we can't establish connection.
		receiveMaximum: defaultReceiveMaximum,
		// Ensures AuthInterval is set for automatic credential refresh
		// otherwise ticker in RefreshAuth() will panic.
		authOptions: &AuthOptions{AuthInterval: defaultAuthInterval},
	}

	c.session = state.NewInMemory()

	c.authErrHandler = func(e error) {
		if e != nil {
			c.error(fmt.Sprintf("error during authentication: %v", e.Error()))
		}
	}

	c.logger = slog.Default()
	// Debug mode is disabled by default.
	c.debugMode = false
}
