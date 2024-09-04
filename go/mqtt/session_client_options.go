package mqtt

import (
	"crypto/tls"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/retrypolicy"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/eclipse/paho.golang/paho"
)

type SessionClientOption func(*SessionClient)

// ******TESTING******

// WithPahoClientFactory sets the pahoClientFactory for the MQTT session client.
// Please note that this is intended for injecting a stub Paho client
// for testing purposes and not for general use.
func WithPahoClientFactory(
	pahoClientFactory func(*paho.ClientConfig) PahoClient,
) SessionClientOption {
	return func(c *SessionClient) {
		c.pahoClientFactory = pahoClientFactory
	}
}

// WithPahoClientConfig set the pahoClientConfig for the pahoClientFactory.
func WithPahoClientConfig(
	pahoClientConfig *paho.ClientConfig,
) SessionClientOption {
	return func(c *SessionClient) {
		c.pahoClientConfig = pahoClientConfig
	}
}

// WithDebugMode set the debugMode flag for the MQTT session client.
func WithDebugMode(
	debugMode bool,
) SessionClientOption {
	return func(c *SessionClient) {
		c.debugMode = debugMode
	}
}

// ******CONNECTION******

// WithConnRetry sets connRetry for the MQTT session client.
func WithConnRetry(
	connRetry retrypolicy.RetryPolicy,
) SessionClientOption {
	return func(c *SessionClient) {
		c.connRetry = connRetry
	}
}

// WithFatalErrHandler sets fatalErrHandler for the MQTT session client.
// The user-defined fatalErrHandler would be called
// when fatal (non-retryable) connection errors happens.
func WithFatalErrHandler(
	fatalErrHandler func(error),
) SessionClientOption {
	return func(c *SessionClient) {
		c.fatalErrHandler = fatalErrHandler
	}
}

// WithAuthErrHandler sets authErrHandler for the MQTT session client.
// The user-defined function authErrHandler would be called
// when auto reauthentication returns an error.
func WithAuthErrHandler(
	authErrHandler func(error),
) SessionClientOption {
	return func(c *SessionClient) {
		c.authErrHandler = authErrHandler
	}
}

// WithShutdownHandler sets shutdownHandler for the MQTT session client.
// The user-defined function will be called
// whenever the session client permanently disconnects
// without automatic reconnection.
func WithShutdownHandler(
	shutdownHandler func(error),
) SessionClientOption {
	return func(c *SessionClient) {
		c.shutdownHandler = shutdownHandler
	}
}

// withConnSettings sets connSettings for the MQTT session client.
// Note that this is not publicly exposed to users.
func withConnSettings(
	connSettings *connectionSettings,
) SessionClientOption {
	return func(c *SessionClient) {
		c.connSettings = connSettings
	}
}

// ensureConnSettings ensures the existence of the connectionSettings.
func ensureConnSettings(c *SessionClient) *connectionSettings {
	if c.connSettings == nil {
		c.connSettings = &connectionSettings{}
	}
	return c.connSettings
}

// WithClientID sets clientID for the connection settings.
func WithClientID(
	clientID string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).clientID = clientID
	}
}

// WithUsername sets the username for the connection settings.
func WithUsername(
	username string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).username = username
	}
}

// WithPassword sets the password for the connection settings.
func WithPassword(
	password []byte,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).password = password
	}
}

// WithPasswordFile sets the passwordFile for the connection settings.
func WithPasswordFile(
	passwordFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).passwordFile = passwordFile
	}
}

// WithCleanStart sets the cleanStart flag for the MQTT connection.
// It can be either true or false for the initial connection,
// however, this option does not affect the cleanStart flag on reconnect.
// It would be false internally for subsequent reconnections
// so we can reuse the old session.
func WithCleanStart(
	cleanStart bool,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).cleanStart = cleanStart
	}
}

// WithKeepAlive sets the keepAlive interval for the MQTT connection.
func WithKeepAlive(
	keepAlive time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).keepAlive = keepAlive
	}
}

// WithSessionExpiry sets the sessionExpiry for the connection settings.
func WithSessionExpiry(
	sessionExpiry time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c)
		// Convert the duration to seconds and then to uint32
		c.connSettings.sessionExpiry = sessionExpiry
		// Provide a convenient way for user to set maximum interval,
		// since if the sessionExpiry is 0xFFFFFFFF (UINT_MAX),
		// the session does not expire.
		if sessionExpiry == -1 {
			c.connSettings.sessionExpiry = time.Duration(
				maxSessionExpiry,
			) * time.Second
		}
	}
}

// WithReceiveMaximum sets the receive maximum for the connection settings.
func WithReceiveMaximum(
	receiveMaximum uint16,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).receiveMaximum = receiveMaximum
	}
}

// WithConnectionTimeout sets the connectionTimeout for the connection settings.
// If connectionTimeout is 0, connection will have no timeout.
// Note the connectionTimeout would work with retrypolicy `connRetry`.
func WithConnectionTimeout(
	connectionTimeout time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).connectionTimeout = connectionTimeout
		if c.connRetry != nil {
			if r, ok := c.connRetry.(*retrypolicy.ExponentialBackoffRetryPolicy); ok {
				retrypolicy.WithTimeout(connectionTimeout)(r)
			}
		} else {
			c.connRetry = retrypolicy.NewExponentialBackoffRetryPolicy(
				retrypolicy.WithTimeout(connectionTimeout),
			)
		}
	}
}

// WithConnectPropertiesUser sets the user properties for the CONNECT packet.
func WithConnectPropertiesUser(
	user map[string]string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).userProperties = user
	}
}

// ******LWT******

// ensureWillMessage ensures the existence of the WillMessage
// for the connectionSettings.
func ensureWillMessage(c *SessionClient) *WillMessage {
	ensureConnSettings(c)
	if c.connSettings.willMessage == nil {
		c.connSettings.willMessage = &WillMessage{}
	}
	return c.connSettings.willMessage
}

// WithWillMessageRetain sets the Retain for the WillMessage.
func WithWillMessageRetain(
	retain bool,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillMessage(c).Retain = retain
	}
}

// WithWillMessageQoS sets the QoS for the WillMessage.
func WithWillMessageQoS(
	qos mqtt.QoS,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillMessage(c).QoS = qos
	}
}

// WithWillMessageTopic sets the Topic for the WillMessage.
func WithWillMessageTopic(
	topic string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillMessage(c).Topic = topic
	}
}

// WithWillMessagePayload sets the Payload for the WillMessage.
func WithWillMessagePayload(
	payload []byte,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillMessage(c).Payload = payload
	}
}

// ensureWillProperties ensures the existence of the WillProperties
// for the connectionSettings.
func ensureWillProperties(c *SessionClient) *WillProperties {
	ensureConnSettings(c)
	if c.connSettings.willProperties == nil {
		c.connSettings.willProperties = &WillProperties{}
	}
	return c.connSettings.willProperties
}

// WithWillPropertiesPayloadFormat sets the PayloadFormat for the WillProperties.
func WithWillPropertiesPayloadFormat(
	payloadFormat mqtt.PayloadFormat,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).PayloadFormat = payloadFormat
	}
}

// WithWillPropertiesWillDelayInterval sets the WillDelayInterval
// for the WillProperties.
func WithWillPropertiesWillDelayInterval(
	willDelayInterval time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).WillDelayInterval = willDelayInterval
	}
}

// WithWillPropertiesMessageExpiry sets the MessageExpiry
// for the WillProperties.
func WithWillPropertiesMessageExpiry(
	messageExpiry time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).MessageExpiry = messageExpiry
	}
}

// WithWillPropertiesContentType sets the ContentType for the WillProperties.
func WithWillPropertiesContentType(
	contentType string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).ContentType = contentType
	}
}

// WithWillPropertiesResponseTopic sets the ResponseTopic
// for the WillProperties.
func WithWillPropertiesResponseTopic(
	responseTopic string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).ResponseTopic = responseTopic
	}
}

// WithWillPropertiesCorrelationData sets the CorrelationData
// for the WillProperties.
func WithWillPropertiesCorrelationData(
	correlationData []byte,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).CorrelationData = correlationData
	}
}

// WithWillPropertiesUser sets the User properties for the WillProperties.
func WithWillPropertiesUser(
	user map[string]string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureWillProperties(c).User = user
	}
}

// ******TLS******

// WithUseTLS enables or disables the use of TLS for the connection settings.
func WithUseTLS(
	useTLS bool,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).useTLS = useTLS
	}
}

// WithTLSConfig sets the TLS configuration for the connection settings.
func WithTLSConfig(
	tlsConfig *tls.Config,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).tlsConfig = tlsConfig
	}
}

// WithCertFile sets the certFile for the connection settings.
func WithCertFile(
	certFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).certFile = certFile
	}
}

// WithKeyFile sets the keyFile for the connection settings.
func WithKeyFile(
	keyFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).keyFile = keyFile
	}
}

// WithKeyFilePassword sets the keyFilePassword for the connection settings.
func WithKeyFilePassword(
	keyFilePassword string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).keyFilePassword = keyFilePassword
	}
}

// WithCaFile sets the caFile for the connection settings.
func WithCaFile(
	caFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).caFile = caFile
	}
}

// WithCaRequireRevocationCheck sets the caRequireRevocationCheck
// for the connection settings.
func WithCaRequireRevocationCheck(
	revocationCheck bool,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureConnSettings(c).caRequireRevocationCheck = revocationCheck
	}
}

// ******AUTH******

// ensureAuth ensures the existence of the AuthOptions
// for the connectionSettings.
func ensureAuth(c *SessionClient) *AuthOptions {
	ensureConnSettings(c)
	if c.connSettings.authOptions == nil {
		c.connSettings.authOptions = &AuthOptions{}
	}
	return c.connSettings.authOptions
}

// WithAuthMethod sets the authMethod for the auth options.
func WithAuthMethod(
	authMethod string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).AuthMethod = authMethod
	}
}

// WithAuthData sets the authData for the auth options.
func WithAuthData(
	authData []byte,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).AuthData = authData
	}
}

// WithSatAuthFile sets the SatAuthFile for the auth options.
func WithSatAuthFile(
	satAuthFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).SatAuthFile = satAuthFile
	}
}

// WithAuthDataProvider sets the authDataProvider for the auth options.
func WithAuthDataProvider(
	authDataProvider AuthDataProvider,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).AuthDataProvider = authDataProvider
	}
}

// WithAuthInterval sets the authInterval for the auth options.
func WithAuthInterval(
	authInterval time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).AuthInterval = authInterval
	}
}

// WithAuthHandler sets the authHandler for the auth options.
func WithAuthHandler(
	authHandler paho.Auther,
) SessionClientOption {
	return func(c *SessionClient) {
		ensureAuth(c).AuthHandler = authHandler
	}
}
