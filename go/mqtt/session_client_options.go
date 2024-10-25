// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"crypto/tls"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
)

type SessionClientOption func(*SessionClient)

// WithLogger sets the logger for the MQTT session client.
func WithLogger(
	l *slog.Logger,
) SessionClientOption {
	return func(c *SessionClient) {
		c.log = internal.Logger{Logger: log.Wrap(l)}
	}
}

// TODO: organize this better

// ******CONNECTION******

// WithConnRetry sets connRetry for the MQTT session client.
func WithConnRetry(
	connRetry retry.Policy,
) SessionClientOption {
	return func(c *SessionClient) {
		c.connRetry = connRetry
	}
}

// withConnectionConfig sets config for the MQTT session client.
// Note that this is not publicly exposed to users.
func withConnectionConfig(
	config *connectionConfig,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config = config
	}
}

// WithClientID sets clientID for the connection settings.
func WithClientID(
	clientID string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.clientID = clientID
	}
}

// UserNameProvider is a function that returns an MQTT User Name Flag and
// User Name. Note that if the return value userNameFlag is false, the return
// value userName is ignored.
type UserNameProvider func(context.Context) (userNameFlag bool, userName string, err error)

// defaultUserNameProvider is a UserNameProvider that returns no MQTT User Name.
// Note that this is unexported because users don't have to use this directly.
// It is used by default if no UserNameProvider is provided by the user.
func defaultUserNameProvider(context.Context) (bool, string, error) {
	return false, "", nil
}

// constantUserNameProvider is a UserNameProvider that returns an unchanging
// User Name. This can be used if the User Name does not need to be updated
// between MQTT connections. Note that this is unexported because users should
// not call this directly and instead use WithUserName.
func constantUserNameProvider(userName string) UserNameProvider {
	return func(context.Context) (bool, string, error) {
		return true, userName, nil
	}
}

// WithUserNameProvider sets the UserNameProvider that the SessionClient uses
// to get the MQTT User Name for each MQTT connection. This is an advanced
// option that most users will not need to use. Consider using WithUsername
// instead.
func WithUserNameProvider(provider UserNameProvider) SessionClientOption {
	return func(c *SessionClient) {
		c.config.userNameProvider = provider
	}
}

// WithUserName sets a constant MQTT User Name for each MQTT connection.
func WithUserName(userName string) SessionClientOption {
	return WithUserNameProvider(constantUserNameProvider(userName))
}

// PasswordProvider is a function that returns an MQTT Password Flag and
// Password. Note that if the return value passwordFlag is false, the return
// value password is ignored.
type PasswordProvider func(context.Context) (passwordFlag bool, password []byte, err error)

// defaultPasswordProvider is a PasswordProvider that returns no MQTT Password.
// Note that this is unexported because users don't have to use this directly.
// It is used by default if no PasswordProvider is provided by the user.
func defaultPasswordProvider(context.Context) (bool, []byte, error) {
	return false, nil, nil
}

// constantPasswordProvider is a PasswordProvider that returns an unchanging
// Password. This can be used if the Password does not need to be updated
// between MQTT connections. Note that this is unexported because users should
// not call this directly and instead use WithPassword.
func constantPasswordProvider(password []byte) PasswordProvider {
	return func(context.Context) (bool, []byte, error) {
		return true, password, nil
	}
}

// filePasswordProvider is a PasswordProvider that reads an MQTT Password from a
// given filename for each MQTT connection. Note that this is unexported because
// users should not call this directly and instead use WithPasswordFile.
func filePasswordProvider(filename string) PasswordProvider {
	return func(context.Context) (bool, []byte, error) {
		data, err := os.ReadFile(filename)
		if err != nil {
			return false, nil, err
		}
		return true, data, nil
	}
}

// WithPasswordProvider sets the PasswordProvider that the SessionClient uses to
// get the MQTT Password for each MQTT connection. This is an advanced option
// that most users will not need to use. Consider using WithPassword or
// WithPasswordFile instead.
func WithPasswordProvider(provider PasswordProvider) SessionClientOption {
	return func(c *SessionClient) {
		c.config.passwordProvider = provider
	}
}

// WithPassword sets a constant MQTT Password for each MQTT connection.
func WithPassword(password []byte) SessionClientOption {
	return WithPasswordProvider(constantPasswordProvider(password))
}

// WithPasswordFile sets up the SessionClient to read an MQTT Password from the
// given filename for each MQTT connection.
func WithPasswordFile(filename string) SessionClientOption {
	return WithPasswordProvider(filePasswordProvider(filename))
}

// WithKeepAlive sets the keepAlive interval for the MQTT connection.
func WithKeepAlive(
	keepAlive time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.keepAlive = keepAlive
	}
}

// WithSessionExpiry sets the sessionExpiry for the connection settings.
func WithSessionExpiry(
	sessionExpiry time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		// Convert the duration to seconds and then to uint32
		c.config.sessionExpiry = sessionExpiry
		// Provide a convenient way for user to set maximum interval,
		// since if the sessionExpiry is 0xFFFFFFFF (UINT_MAX),
		// the session does not expire.
		if sessionExpiry == -1 {
			c.config.sessionExpiry = time.Duration(
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
		c.config.receiveMaximum = receiveMaximum
	}
}

// WithConnectionTimeout sets the connectionTimeout for the connection settings.
// If connectionTimeout is 0, connection will have no timeout.
// Note the connectionTimeout would work with connRetry.
func WithConnectionTimeout(
	connectionTimeout time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.connectionTimeout = connectionTimeout
		if c.connRetry != nil {
			if r, ok := c.connRetry.(*retry.ExponentialBackoff); ok {
				r.Timeout = connectionTimeout
			}
		} else {
			c.connRetry = &retry.ExponentialBackoff{
				Timeout: connectionTimeout,

				// TODO: This only works if the options are called in the right
				// order.
				Logger: c.log.Wrapped,
			}
		}
	}
}

// WithConnectPropertiesUser sets the user properties for the CONNECT packet.
func WithConnectPropertiesUser(
	user map[string]string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.userProperties = user
	}
}

// ******LWT******

// ensureWillMessage ensures the existence of the WillMessage
// for the connectionSettings.
func ensureWillMessage(c *SessionClient) *WillMessage {
	if c.config.willMessage == nil {
		c.config.willMessage = &WillMessage{}
	}
	return c.config.willMessage
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
	qos byte,
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
	if c.config.willProperties == nil {
		c.config.willProperties = &WillProperties{}
	}
	return c.config.willProperties
}

// WithWillPropertiesPayloadFormat sets the PayloadFormat for the
// WillProperties.
func WithWillPropertiesPayloadFormat(
	payloadFormat byte,
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
		c.config.useTLS = useTLS
	}
}

// WithTLSConfig sets the TLS configuration for the connection settings.
// Note that this only has an effect if the server URL scheme is "mqtts", "tls",
// or "ssl".
func WithTLSConfig(
	tlsConfig *tls.Config,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.tlsConfig = tlsConfig
	}
}

// WithCertFile sets the certFile for the connection settings.
func WithCertFile(
	certFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.certFile = certFile
	}
}

// WithKeyFile sets the keyFile for the connection settings.
func WithKeyFile(
	keyFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.keyFile = keyFile
	}
}

// WithKeyFilePassword sets the keyFilePassword for the connection settings.
func WithKeyFilePassword(
	keyFilePassword string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.keyFilePassword = keyFilePassword
	}
}

// WithCaFile sets the caFile for the connection settings.
func WithCaFile(
	caFile string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.caFile = caFile
	}
}

// WithCaRequireRevocationCheck sets the caRequireRevocationCheck
// for the connection settings.
func WithCaRequireRevocationCheck(
	revocationCheck bool,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.caRequireRevocationCheck = revocationCheck
	}
}

// ******TESTING******

// WithPahoConstructor replaces the default Paho constructor with a custom one
// for testing.
func WithPahoConstructor(
	pahoConstructor PahoConstructor,
) SessionClientOption {
	return func(c *SessionClient) {
		c.pahoConstructor = pahoConstructor
	}
}
