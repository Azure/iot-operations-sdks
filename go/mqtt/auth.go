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

	go func() {
		ctx, cancel := current.Down.With(context.Background())
		defer cancel()

		values, err := c.config.authProvider.InitiateAuthExchange(true)
		if err != nil {
			// using context.TODO() here because we are not passing a context
			// into InitiateAuthExchange and we want to review logging contexts
			// to ensure they get torn down with the client.
			c.log.Error(context.TODO(), err)
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
		_, err = current.Client.Authenticate(ctx, packet)
		if err != nil {
			c.log.Error(ctx, err)
		}
	}()
}

// Implements paho.Auther.
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
	a.c.config.authProvider.AuthSuccess(a.c.requestReauthentication)
}
