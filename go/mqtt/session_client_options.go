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

// ******CLEAN START******

// WithFirstConnectionCleanStart sets the value of Clean Start in the CONNECT
// packet for the first connection. Note that Clean Start will always be false
// on reconnections.
//
// This setting is true by default, and it should not be changed unless you are
// aware of the implications. If there is a possibility of a session on the
// MQTT server for this Client ID with inflight QoS 1+ PUBLISHes or QoS 2
// SUBSCRIBEs, it may result in message loss and/or MQTT protocol violations.
func WithFirstConnectionCleanStart(
	firstConnectionCleanStart bool,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.firstConnectionCleanStart = firstConnectionCleanStart
	}
}

// ******RETRY POLICY******

// WithConnRetry sets the connection retry policy for the MQTT session client.
func WithConnRetry(
	connRetry retry.Policy,
) SessionClientOption {
	return func(c *SessionClient) {
		c.connRetry = connRetry
	}
}

// ******CLIENT IDENTIFIER******

// WithClientID sets MQTT Client Identifier.
func WithClientID(
	clientID string,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.clientID = clientID
	}
}

// ******USER NAME******

// UserNameProvider is a function that returns an MQTT User Name and User Name
// Flag. Note that if the return value userNameFlag is false, the return value
// userName is ignored.
type UserNameProvider func(context.Context) (userName string, userNameFlag bool, err error)

// WithUserName sets the UserNameProvider that the SessionClient uses to get the
// MQTT User Name for each MQTT connection.
func WithUserName(provider UserNameProvider) SessionClientOption {
	return func(c *SessionClient) {
		c.config.userNameProvider = provider
	}
}

// defaultUserName is a UserNameProvider implementation that returns no MQTT
// User Name. Note that this is unexported because users don't have to use this
// directly. It is used by default if no UserNameProvider is provided by the
// user.
func defaultUserName(context.Context) (string, bool, error) {
	return "", false, nil
}

// ConstantUserName is a UserNameProvider implementation that returns an
// unchanging User Name. This can be used if the User Name does not need to be
// updated between MQTT connections.
func ConstantUserName(userName string) UserNameProvider {
	return func(context.Context) (string, bool, error) {
		return userName, true, nil
	}
}

// ******PASSWORD******

// PasswordProvider is a function that returns an MQTT Password and Password
// Flag. Note that if the return value passwordFlag is false, the return value
// password is ignored.
type PasswordProvider func(context.Context) (password []byte, passwordFlag bool, err error)

// WithPassword sets the PasswordProvider that the SessionClient uses to get the
// MQTT Password for each MQTT connection.
func WithPassword(provider PasswordProvider) SessionClientOption {
	return func(c *SessionClient) {
		c.config.passwordProvider = provider
	}
}

// defaultPassword is a PasswordProvider implementation that returns no MQTT
// Password. Note that this is unexported because users don't have to use this
// directly. It is used by default if no PasswordProvider is provided by the
// user.
func defaultPassword(context.Context) ([]byte, bool, error) {
	return nil, false, nil
}

// ConstantPassword is a PasswordProvider implementation that returns an
// unchanging Password. This can be used if the Password does not need to be
// updated between MQTT connections.
func ConstantPassword(password []byte) PasswordProvider {
	return func(context.Context) ([]byte, bool, error) {
		return password, true, nil
	}
}

// FilePassword is a PasswordProvider implementation that reads an MQTT Password
// from a given filename for each MQTT connection.
func FilePassword(filename string) PasswordProvider {
	return func(context.Context) ([]byte, bool, error) {
		data, err := os.ReadFile(filename)
		if err != nil {
			return nil, false, err
		}
		return data, true, nil
	}
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

// WithSessionExpiryInterval sets the MQTT Session Expiry Interval.
func WithSessionExpiryInterval(
	sessionExpiryInterval uint32,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.sessionExpiryInterval = sessionExpiryInterval
	}
}

// ******RECEIVE MAXIMUM******

// WithReceiveMaximum sets the MQTT client-side Receive Maximum.
func WithReceiveMaximum(
	receiveMaximum uint16,
) SessionClientOption {
	return func(c *SessionClient) {
		c.config.receiveMaximum = receiveMaximum
	}
}

// ******CONNECTION TIMEOUT******

// WithConnectionTimeout sets the connection timeout.
func WithConnectionTimeout(
	connectionTimeout time.Duration,
) SessionClientOption {
	// TODO: this is currently treated as the timeout for a single connection
	// attempt. Once discussion on this occurs, ensure this is aligned with the
	// other session client implementations and document the specific meaning
	// of "connection timeout" here.
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
