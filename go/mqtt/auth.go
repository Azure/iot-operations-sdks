// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
	"github.com/eclipse/paho.golang/paho"
)

func (c *SessionClient) requestReauth() {
	current := c.conn.Current()

	if current.Client == nil {
		// The connection is down, so the reauth request is irrelevant at this
		// point.
		return
	}

	go func() {
		ctx, cancel := current.Down.With(context.Background())
		defer cancel()

		values, err := c.options.Auth.InitiateAuth(true)
		if err != nil {
			c.log.Error(ctx, err)
			return
		}

		packet := &paho.Auth{
			ReasonCode: authReauthenticate,
			Properties: &paho.AuthProperties{
				AuthData:   values.AuthData,
				AuthMethod: values.AuthMethod,
			},
		}

		// NOTE: We ignore the error return of client.Authenticate() because if
		// it fails, there's nothing we can do except let the client eventually
		// disconnect and try to reconnect.
		c.log.Packet(ctx, "auth", packet)
		resp, err := current.Client.Authenticate(ctx, packet)
		c.log.Packet(ctx, "auth response", resp)
		if err != nil {
			c.log.Error(ctx, err)
		}
	}()
}

// Implements paho.Auther.
type pahoAuther struct{ *SessionClient }

func (a *pahoAuther) Authenticate(packet *paho.Auth) *paho.Auth {
	values, err := a.options.Auth.ContinueAuth(
		&auth.Values{
			AuthMethod: packet.Properties.AuthMethod,
			AuthData:   packet.Properties.AuthData,
		},
	)
	if err != nil {
		// returning an AUTH packet with zero values rather than nil because
		// Paho dereferences this return value without a nil check. Since we are
		// returning an invalid auth packet, we will eventually get disconnected
		// by the server anyway.
		return &paho.Auth{}
	}
	return &paho.Auth{
		ReasonCode: authContinueAuthentication,
		Properties: &paho.AuthProperties{
			AuthMethod: values.AuthMethod,
			AuthData:   values.AuthData,
		},
	}
}

func (a *pahoAuther) Authenticated() {
	a.options.Auth.AuthSuccess(a.requestReauth)
}
