// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

import "errors"

// Values from AUTH packets sent to and received from the MQTT server.
type Values struct {
	AuthMethod string
	AuthData   []byte
}

// Provider implements an MQTT enhanced authentication exchange.
type Provider interface {
	// InitiateAuth is called by the session client when an enhanced auth
	// exchange is initiated. An enhanced auth exchange is initiated when a new
	// MQTT connection is being created or when the Provider implementation
	// calls the requestReauth callback passed to it via AuthSuccess.
	//
	// `reauth` is true if this is a reauthentication on a live MQTT connection
	// and false it is on new MQTT connection.
	//
	// The return value is a pointer to a Values struct that contains values
	// that will be sent to the server via a CONNECT or AUTH packet.
	InitiateAuth(reauth bool) (*Values, error)

	// ContinueAuth is called by the session client when it receives an AUTH
	// packet from the server with reason code 0x18 (continue authentication).
	//
	// `values` contains the the values from the aforementioned AUTH packet.
	//
	// The return value is a pointer to to an Values struct that contains
	// values that will be sent to the server via an AUTH packet for this round
	// of the enhanced auth exchange.
	ContinueAuth(values *Values) (*Values, error)

	// AuthSuccess is called by the session client when it receives a CONNACK
	// or AUTH packet with a success reason code (0x00) after an enhanced auth
	// exchange was initiated.
	//
	// `requestReauth` is a callback that the Provider implementation may call
	// to tell the session client to initiate a reauthentication on the live
	// MQTT connection. Note that this function is valid for use for the entire
	// lifetime of the session client.
	AuthSuccess(requestReauth func())
}

var ErrUnexpected = errors.New("unexpected call to auth provider")
