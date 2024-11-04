// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
	"github.com/eclipse/paho.golang/paho"
)

func (c *SessionClient) requestReauthentication() {
	current := c.conn.Current()

	if current.Client == nil {
		// The connection is down, so the reauth request is irrelevant at this
		// point.
		return
	}

	ctx, cancel := context.WithCancel(context.Background())
	go func() {
		defer cancel()

		select {
		case <-current.Down():
		case <-ctx.Done():
		}
	}()

	go func() {
		defer cancel()

		values, err := c.config.authProvider.InitiateAuthExchange(
			true,
			c.requestReauthentication,
		)
		if err != nil {
			// TODO: log this error
			return
		}

		packet := &paho.Auth{
			ReasonCode: authReauthenticate,
			Properties: &paho.AuthProperties{
				AuthData:   values.AuthenticationData,
				AuthMethod: values.AuthenticationMethod,
			},
		}

		// NOTE: we ignore the return values of client.Authenticate() because
		// if it fails, there's nothing we can do except let the client
		// eventually disconnect and try to reconnect.
		_, _ = current.Client.Authenticate(ctx, packet)

		// TODO: log any errors from client.Authenticate()
	}()
}

type pahoAuther struct {
	c *SessionClient
}

func (a *pahoAuther) Authenticate(packet *paho.Auth) *paho.Auth {
	values, err := a.c.config.authProvider.ContinueAuthExchange(
		&auth.Values{
			AuthenticationMethod: packet.Properties.AuthMethod,
			AuthenticationData:   packet.Properties.AuthData,
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
			AuthMethod: values.AuthenticationMethod,
			AuthData:   values.AuthenticationData,
		},
	}
}

func (a *pahoAuther) Authenticated() {
	a.c.config.authProvider.AuthSuccess()
}
