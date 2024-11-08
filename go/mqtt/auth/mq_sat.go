// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

import (
	"fmt"
	"os"
)

// MQServiceAccountToken impelements an EnhancedAuthenticationProvider that
// reads an a Kubernetes Service Account token from the given filename and
// puts it into MQTT Enhanced Authentication values for Azure IoT Operations MQ.
type MQServiceAccountToken struct {
	filename string
}

func NewMQServiceAccountToken(filename string) *MQServiceAccountToken {
	return &MQServiceAccountToken{filename: filename}
}

func (sat *MQServiceAccountToken) InitiateAuthExchange(
	reauthentication bool,
) (*Values, error) {
	if reauthentication {
		// TODO: remove this error when we implement re-authentication
		return nil, fmt.Errorf(
			"reauthentication request received when it wasn't implemented yet",
		)
	}

	token, err := os.ReadFile(sat.filename)
	if err != nil {
		return nil, err
	}
	return &Values{
		AuthenticationMethod: "K8S-SAT",
		AuthenticationData:   token,
	}, nil
}

func (*MQServiceAccountToken) ContinueAuthExchange(
	_ *Values,
) (*Values, error) {
	return nil, fmt.Errorf(
		"ContinueAuthExchange called on MQServiceAccountToken, but multiple rounds of exchange was not expected",
	)
}

func (*MQServiceAccountToken) AuthSuccess(func()) {
	// TODO: start a timer or a file watcher for re-authentication. It is not
	// strictly necessary for the session client to function with MQ, but it
	// will prevent reconnections from ocurring when the token expires.
}
