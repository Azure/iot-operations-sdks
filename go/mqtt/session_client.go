package mqtt

import (
	"crypto/tls"
	"fmt"
	"log/slog"
	"sync"
	"sync/atomic"
	"time"

	"github.com/eclipse/paho.golang/paho"
	"github.com/eclipse/paho.golang/paho/session"
	"github.com/eclipse/paho.golang/paho/session/state"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type (
	// SessionClient implements an MQTT Session client
	// supporting MQTT v5 with QoS 0 and QoS 1.
	// TODO: Add support for QoS 2.
	SessionClient struct {
		// **Paho MQTTv5 client**
		pahoClient PahoClient

		// **Connection**
		connSettings *connectionSettings
		connRetry    retrypolicy.RetryPolicy

		// Count of the successful connections.
		connCount int64

		// Connection status signal.
		isConnected atomic.Bool

		// Connection shut down channel.
		// (Go revive) No nested structs are allowed so we use error here.
		connStopC internal.BufferChan[error]

		// Allowing session restoration upon reconnection.
		session session.SessionManager

		// **Management**
		// Subscriptions by topic filter.
		subscriptions   map[string]*subscription
		subscriptionsMu sync.RWMutex

		// Queue for storing pending packets when the connection fails.
		pendingPackets *internal.Queue[queuedPacket]

		// If pendingPackets is being processed,
		// block other packets sending operations.
		packetQueueMu   sync.Mutex
		packetQueueCond sync.Cond

		// The user-defined function would be called
		// when fatal (non-retryable) connection errors happens.
		fatalErrHandler func(error)
		// The user-defined function would be called
		// when auto reauthentication returns an error.
		authErrHandler func(error)

		// The user-defined function will be called
		// whenever the session client permanently disconnects
		// without automatic reconnection.
		shutdownHandler func(error)

		// Error channel for connection errors from Paho.
		// Errors during connection will be captured in this channel.
		// A message on the clientErrC indicates
		// a disconnection has occurred or will occur.
		// Only 1 error for 1 connection at the time to make sure
		// error would be handled immediately after sending.
		clientErrC internal.BufferChan[error]

		// Error channel for translated disconnect errors
		// from Paho.OnServerDisconnect callback,
		// indicating an abnormal disconnection event
		// from the server, thus potentially prompting a retry.
		disconnErrC internal.BufferChan[error]

		logger *slog.Logger

		// **Testing**
		// Factory for initializing the Paho Client.
		// Currently, this is intended only for testing convenience.
		pahoClientFactory func(*paho.ClientConfig) PahoClient
		// Nil by default since it's only needed for stub client.
		pahoClientConfig *paho.ClientConfig

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

	subscription struct {
		*SessionClient
		topic   string
		handler mqtt.MessageHandler
	}

	// queuedPacket would hold packets such as
	// paho.Subscribe, paho.Publish, or paho.Unsubscribe,
	// and other necessary information.
	queuedPacket struct {
		packet any
		errC   chan error
		// For paho.Subscribe
		*subscription
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
	atomic.StoreInt64(&c.connCount, 0)
	c.setDisconnected()
	c.connRetry = retrypolicy.NewExponentialBackoffRetryPolicy()
	c.connStopC = *internal.NewBufferChan[error](1)
	c.connSettings = &connectionSettings{
		clientID: randomClientID(),
		// If receiveMaximum is 0, we can't establish connection.
		receiveMaximum: defaultReceiveMaximum,
		// Ensures AuthInterval is set for automatic credential refresh
		// otherwise ticker in RefreshAuth() will panic.
		authOptions: &AuthOptions{AuthInterval: defaultAuthInterval},
	}

	c.session = state.NewInMemory()

	c.subscriptions = map[string]*subscription{}
	c.pendingPackets = internal.NewQueue[queuedPacket](maxPacketQueueSize)
	c.packetQueueCond = *sync.NewCond(&c.packetQueueMu)

	c.fatalErrHandler = func(e error) {
		if e != nil {
			c.error(fmt.Sprintf("fatal error occurred: %v", e.Error()))
		}
	}
	c.authErrHandler = func(e error) {
		if e != nil {
			c.error(fmt.Sprintf("error during authentication: %v", e.Error()))
		}
	}
	c.shutdownHandler = func(e error) {
		c.info("client has been shut down")
		if e != nil {
			c.info(fmt.Sprintf("client shutdown reason: %v", e.Error()))
		}
	}
	c.clientErrC = *internal.NewBufferChan[error](1)
	c.disconnErrC = *internal.NewBufferChan[error](1)

	c.pahoClientFactory = func(config *paho.ClientConfig) PahoClient {
		return paho.NewClient(*config)
	}

	c.logger = slog.Default()
	// Debug mode is disabled by default.
	c.debugMode = false
}

// ensureClient checks that the session client is initialized.
func (c *SessionClient) ensureClient() error {
	if c == nil {
		err := &errors.Error{
			Kind:    errors.StateInvalid,
			Message: "session client was not initialized",
		}
		c.error(err.Error())
		return err
	}
	return nil
}

// ensureClient checks that the Paho client is initialized.
func (c *SessionClient) ensurePahoClient() error {
	if c == nil {
		err := &errors.Error{
			Kind:    errors.StateInvalid,
			Message: "Paho client was not initialized",
		}
		c.error(err.Error())
		return err
	}
	return nil
}
