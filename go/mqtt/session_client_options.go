// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"log/slog"
	"maps"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
	"github.com/Azure/iot-operations-sdks/go/mqtt/retry"
)

type (
	// SessionClientOption represents a single option for the session client.
	SessionClientOption interface{ sessionClient(*SessionClientOptions) }

	// SessionClientOptions are the resolved options for the session client.
	SessionClientOptions struct {
		CleanStart               bool
		KeepAlive                uint16
		SessionExpiry            uint32
		ReceiveMaximum           uint16
		ConnectUserProperties    map[string]string
		DisableAIOBrokerFeatures bool

		ConnectionRetry   retry.Policy
		ConnectionTimeout time.Duration

		Username UsernameProvider
		Password PasswordProvider
		Auth     auth.Provider

		Logger *slog.Logger
	}

	// WithConnectionTimeout sets the connection timeout for a single connection
	// attempt. If a timeout is desired for the entire connection process, it
	// should be specified via the connection retry policy.
	WithConnectionTimeout time.Duration

	// WithCleanStart sets whether the initial connection will be made without
	// retaining any existing session state. This is by definition set to false
	// for any reconnections.
	WithCleanStart bool

	// WithKeepAlive sets the keep-alive interval (in seconds).
	WithKeepAlive uint16

	// WithSessionExpiry sets the session expiry interval (in seconds).
	WithSessionExpiry uint32

	// WithReceiveMaximum sets the client-side receive maximum.
	WithReceiveMaximum uint16

	// WithConnectUserProperties sets the user properties for the CONNECT
	// packet.
	WithConnectUserProperties map[string]string

	// WithDisableAIOBrokerFeatures disables behavior specific to the AIO
	// Broker. Only use this option if you are using another broker and
	// encounter failures.
	WithDisableAIOBrokerFeatures bool

	// WithUsername sets the UsernameProvider that the session client uses to
	// get the username for each connection.
	WithUsername UsernameProvider

	// WithPassword sets the PasswordProvider that the session client uses to
	// get the password for each connection.
	WithPassword PasswordProvider

	withConnectionRetry struct{ retry.Policy }
	withAuth            struct{ auth.Provider }
	withLogger          struct{ *slog.Logger }
)

// Apply resolves the provided list of options.
func (o *SessionClientOptions) Apply(
	opts []SessionClientOption,
	rest ...SessionClientOption,
) {
	for opt := range options.Apply[SessionClientOption](opts, rest...) {
		opt.sessionClient(o)
	}
}

func (o *SessionClientOptions) sessionClient(opt *SessionClientOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithConnectionTimeout) sessionClient(opt *SessionClientOptions) {
	opt.ConnectionTimeout = time.Duration(o)
}

func (o WithCleanStart) sessionClient(opt *SessionClientOptions) {
	opt.CleanStart = bool(o)
}

func (o WithKeepAlive) sessionClient(opt *SessionClientOptions) {
	opt.KeepAlive = uint16(o)
}

func (o WithSessionExpiry) sessionClient(opt *SessionClientOptions) {
	opt.SessionExpiry = uint32(o)
}

func (o WithReceiveMaximum) sessionClient(opt *SessionClientOptions) {
	opt.ReceiveMaximum = uint16(o)
}

func (o WithConnectUserProperties) sessionClient(opt *SessionClientOptions) {
	if opt.ConnectUserProperties == nil {
		opt.ConnectUserProperties = make(map[string]string, len(o))
	}
	maps.Copy(opt.ConnectUserProperties, o)
}

func (o WithDisableAIOBrokerFeatures) sessionClient(opt *SessionClientOptions) {
	opt.DisableAIOBrokerFeatures = bool(o)
}

func (o WithUsername) sessionClient(opt *SessionClientOptions) {
	opt.Username = UsernameProvider(o)
}

func (o WithPassword) sessionClient(opt *SessionClientOptions) {
	opt.Password = PasswordProvider(o)
}

// WithConnectionRetry sets the connection retry policy for the session client.
func WithConnectionRetry(policy retry.Policy) SessionClientOption {
	return withConnectionRetry{policy}
}

func (o withConnectionRetry) sessionClient(opt *SessionClientOptions) {
	opt.ConnectionRetry = o.Policy
}

// WithAuth sets the enhanced authentication provider for the session client.
func WithAuth(provider auth.Provider) SessionClientOption {
	return withAuth{provider}
}

func (o withAuth) sessionClient(opt *SessionClientOptions) {
	opt.Auth = o.Provider
}

// WithLogger sets the logger for the session client.
func WithLogger(log *slog.Logger) SessionClientOption {
	return withLogger{log}
}

func (o withLogger) sessionClient(opt *SessionClientOptions) {
	opt.Logger = o.Logger
}
