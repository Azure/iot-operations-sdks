// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/eclipse/paho.golang/paho"
)

// AuthValues contains values from AUTH packets sent to and received from
// the MQTT server
type AuthValues struct {
	AuthenticationMethod string
	AuthenticationData   []byte
}

type EnhancedAuthenticationProvider interface {
	// InitiateAuthExchange is called by the SessionClient when an enhanced
	// authentication exchange is initiated. An enhanced authentication exchange
	// is initiated when a new MQTT connection is being created or when the
	// implementation of the EnhancedAuthenticationProvider calls the
	// requestReauthentication function passed to it from previous calls to
	// to InitiateAuthentication.
	//
	// reauthentication is true if this is a reauthentication on a live MQTT
	// connection and false it is on new MQTT connection.
	//
	// requestReauthentication is a function that the implementation of
	// EnhancedAuthenticationProvider may call to tell the SessionClient to
	// initiate a reauthentication on the live MQTT connection. Note that this
	// function is valid for use for the entire lifetime of the SessionClient.
	//
	// The return value is a pointer to an AuthValues struct that contains
	// values that will be sent to the server via a CONNECT or AUTH packet.
	InitiateAuthExchange(ctx context.Context, reauthentication bool, requestReauthentication func()) (*AuthValues, error)

	// ContinueAuthExchange is called by the SessionClient when it receives an
	// AUTH packet from the server with reason code 0x18 (Continue
	// authentication).
	//
	// values contains the the values from the aforementioned AUTH packet.
	//
	// The return value is a pointer to to an AuthValues struct that contains
	// values that will be sent to the server via an AUTH packet for this round
	// of the enhanced authentication exchange.
	ContinueAuthExchange(ctx context.Context, values *AuthValues) (*AuthValues, error)

	// AuthSuccess is called by the SessionClient when it receives a CONNACK
	// or AUTH packet with a success reason code (0x00) after an enhanced
	// authentication exchange was initiated.
	AuthSuccess()
}

type pahoAuther struct {
	c *SessionClient

	mu       sync.Mutex
	inflight bool
	done     chan struct{}
	current  internal.CurrentConnection[PahoClient]
}

func newPahoAuther() *pahoAuther {
	a := &pahoAuther{done: make(chan struct{})}
	close(a.done)
	return a
}

func (a *pahoAuther) clear() {
	a.mu.Lock()
	defer a.mu.Unlock()
	if !a.inflight {
		return
	}
	close(a.done)
	a.inflight = false
}

func (a *pahoAuther) requestReauthentication() {
	go func() {
		a.mu.Lock()
		defer a.mu.Unlock()

		if a.inflight {
			return
		}
		a.inflight = true
		// TODO: what to do with done chan.

		values, err := a.c.config.authProvider.InitiateAuthExchange(
			context.TODO(),
			true,
			func() { a.requestReauthentication(a.c.conn.Current()) },
		)
		if err != nil {
			return
		}

		// TODO: we are not holding lock here
		current.Client.Authenticate(context.TODO(), &paho.Auth{
			Properties: &paho.AuthProperties{
				AuthMethod: values.AuthenticationMethod,
				AuthData:   values.AuthenticationData,
			},
		})
	}()

	go func() {
		for pa
	}()
}

func (a *pahoAuther) Authenticate(auth *paho.Auth) *paho.Auth {
	values, err := a.c.config.authProvider.ContinueAuthExchange(
		context.TODO(),
		&AuthValues{
			AuthenticationMethod: auth.Properties.AuthMethod,
			AuthenticationData:   auth.Properties.AuthData,
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
		Properties: &paho.AuthProperties{
			AuthMethod: values.AuthenticationMethod,
			AuthData:   values.AuthenticationData,
		},
	}
}

func (a *pahoAuther) Authenticated() {
	a.c.config.authProvider.AuthSuccess()
}
