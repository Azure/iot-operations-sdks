// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/eclipse/paho.golang/paho"
)

// WithReauthData sets the auth data for reauthentication.
type WithReauthData []byte

// Reauthenticate initiates credential reauthentication with the server.
// It sends the initial Auth packet to start reauthentication, then relies
// on the user's AuthHandler to manage further requests from the server
// until a successful Auth packet is passed back or a Disconnect is received.
func (c *SessionClient) Reauthenticate(
	ctx context.Context,
	opts ...AuthOption,
) error {
	var opt AuthOptions
	opt.Apply(opts)

	// TODO: Due to a limitation in Paho, the AuthHandler is tied to
	// the Paho client. We SHOULD allow updating the handler if needed.
	if opt.AuthHandler != nil {
		return &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "AuthHandler can't be updated for reauthentication",
			PropertyName:  "AuthHandler",
			PropertyValue: opt.AuthHandler,
		}
	}

	// https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901256
	// AuthMethod cannot be updated:
	// if the initial CONNECT packet included an Authentication Method property
	// then all AUTH packets, and any successful CONNACK packet
	// MUST include an Authentication Method Property
	// with the same value as in the CONNECT packet [MQTT-4.12.0-5].
	if opt.AuthMethod != "" {
		return &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "AuthMethod can't be updated for reauthentication",
			PropertyName:  "AuthMethod",
			PropertyValue: opt.AuthMethod,
		}
	}

	// If multiple sources are provided, the priority is:
	// AuthDataProvider > SatAuthFile > AuthData.
	switch {
	case opt.AuthDataProvider != nil:
		WithAuthDataProvider(opt.AuthDataProvider)(c)
		WithAuthData(opt.AuthDataProvider(ctx))(c)
	case opt.SatAuthFile != "":
		WithSatAuthFile(opt.SatAuthFile)(c)
		data, err := readFileAsBytes(opt.SatAuthFile)
		if err != nil {
			return &errors.Error{
				Kind:          errors.ConfigurationInvalid,
				Message:       "cannot read auth data from target file",
				PropertyName:  "SatAuthFile",
				PropertyValue: opt.SatAuthFile,
				NestedError:   err,
			}
		}
		WithAuthData(data)(c)
	case opt.AuthData != nil:
		WithAuthData(opt.AuthData)(c)
	}

	// Build MQTT Auth packet.
	auth := &paho.Auth{
		ReasonCode: byte(reauthenticate),
		Properties: &paho.AuthProperties{
			AuthMethod: c.connSettings.authOptions.AuthMethod,
			AuthData:   c.connSettings.authOptions.AuthData,
		},
	}

	// Connection lost; we can't buffer Auth packet for reconnection.
	if func() bool {
		c.connectionMu.Lock()
		defer c.connectionMu.Unlock()
		return !c.connected
	}() {
		return &errors.Error{
			Kind:    errors.ExecutionException,
			Message: "connection lost during reauthentication",
		}
	}

	// Execute the authentication.
	return pahoAuth(ctx, c.pahoClient, auth)
}

// autoAuth periodically reauthenticates the client at intervals
// specified by AuthInterval in connection settings.
func (c *SessionClient) autoAuth(ctx context.Context) {
	ticker := time.NewTicker(c.connSettings.authOptions.AuthInterval)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			c.log.Info(ctx, "start reauthentication")
			if err := c.Reauthenticate(ctx); err != nil {
				c.log.Error(ctx, err)
			}
		case <-ctx.Done():
			c.log.Info(ctx, "stop auto reauthentication on client shutdown")
			return
		}
	}
}

func (o WithReauthData) authenticate(opt *AuthOptions) {
	opt.AuthData = []byte(o)
}

// Apply resolves the provided list of options.
func (o *AuthOptions) Apply(
	opts []AuthOption,
	rest ...AuthOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.authenticate(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.authenticate(o)
		}
	}
}

func (o *AuthOptions) authenticate(opt *AuthOptions) {
	if o != nil {
		*opt = *o
	}
}
