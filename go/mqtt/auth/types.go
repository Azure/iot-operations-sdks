// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

// Values contains values from AUTH packets sent to and received from
// the MQTT server.
type Values struct {
	AuthenticationMethod string
	AuthenticationData   []byte
}

type Provider interface {
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
	// The return value is a pointer to an Values struct that contains values
	// that will be sent to the server via a CONNECT or AUTH packet.
	InitiateAuthExchange(reauthentication bool) (*Values, error)

	// ContinueAuthExchange is called by the SessionClient when it receives an
	// AUTH packet from the server with reason code 0x18 (Continue
	// authentication).
	//
	// values contains the the values from the aforementioned AUTH packet.
	//
	// The return value is a pointer to to an Values struct that contains
	// values that will be sent to the server via an AUTH packet for this round
	// of the enhanced authentication exchange.
	ContinueAuthExchange(values *Values) (*Values, error)

	// AuthSuccess is called by the SessionClient when it receives a CONNACK
	// or AUTH packet with a success reason code (0x00) after an enhanced
	// authentication exchange was initiated.
	//
	// requestReauthentication is a function that the implementation of
	// EnhancedAuthenticationProvider may call to tell the SessionClient to
	// initiate a reauthentication on the live MQTT connection. Note that this
	// function is valid for use for the entire lifetime of the SessionClient.
	AuthSuccess(requestReauthentication func())
}
