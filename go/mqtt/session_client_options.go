// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
)

type SessionClientOption func(*SessionClient)

// ******LOGGER******

// WithLogger sets the logger for the MQTT session client.
func WithLogger(
	l *slog.Logger,
) SessionClientOption {
	return func(c *SessionClient) {
		c.log = internal.Logger{Logger: log.Wrap(l)}
	}
}

// ******INTERNAL CONFIG******

// withConnectionConfig sets config for the MQTT session client.
// Note that this is not publicly exposed to users because the connectionConfig
// should not be directly set by users.
func withConnectionConfig(
	config *connectionConfig,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config = config
	}
}

// ******RETRY POLICY******

// WithConnRetry sets connRetry for the MQTT session client.
func WithConnRetry(
	connRetry retry.Policy,
) SessionClientOption {
	return func(c *SessionClient) {
		c.connRetry = connRetry
	}
}

// ******CLIENT ID******

// WithClientID sets clientID for the connection settings.
func WithClientID(
	clientID string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.clientID = clientID
	}
}

// ******USER NAME******

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

// ******PASSWORD******

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

// ******KEEP ALIVE******

// WithKeepAlive sets the keepAlive interval for the MQTT connection.
func WithKeepAlive(
	keepAlive uint16,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.keepAlive = keepAlive
	}
}

// ******SESSION EXPIRY INTERVAL******

// WithSessionExpiryInterval sets the sessionExpiry for the connection settings.
func WithSessionExpiryInterval(
	sessionExpiryInterval uint32,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.sessionExpiryInterval = sessionExpiryInterval
	}
}

// ******RECEIVE MAXIMUM******

// WithReceiveMaximum sets the receive maximum for the connection settings.
func WithReceiveMaximum(
	receiveMaximum uint16,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.receiveMaximum = receiveMaximum
	}
}

// ******CONNECTION TIMEOUT******

// WithConnectionTimeout sets the connectionTimeout for the connection settings.
// If connectionTimeout is 0, connection will have no timeout.
// Note the connectionTimeout would work with connRetry.
func WithConnectionTimeout(
	connectionTimeout time.Duration,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.connectionTimeout = connectionTimeout
	}
}

// ******CONNECT USER PROPERTIES******

// WithConnectPropertiesUser sets the user properties for the CONNECT packet.
func WithConnectPropertiesUser(
	userProperties map[string]string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.userProperties = userProperties
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
