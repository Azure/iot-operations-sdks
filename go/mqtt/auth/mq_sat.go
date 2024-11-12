// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package auth

import "os"

// AIOServiceAccountToken impelements an enhanced authentication provider that
// reads a Kubernetes Service Account Token for the AIO Broker.
type AIOServiceAccountToken struct {
	filename string
}

// NewAIOServiceAccountToken creates a new AIO SAT auth provider from the given
// filename.
func NewAIOServiceAccountToken(filename string) *AIOServiceAccountToken {
	return &AIOServiceAccountToken{filename: filename}
}

func (sat *AIOServiceAccountToken) InitiateAuth(
	reauth bool,
) (*Values, error) {
	if reauth {
		// TODO: remove this error when we implement re-authentication
		return nil, ErrUnexpected
	}

	token, err := os.ReadFile(sat.filename)
	if err != nil {
		return nil, err
	}
	return &Values{
		AuthMethod: "K8S-SAT",
		AuthData:   token,
	}, nil
}

func (*AIOServiceAccountToken) ContinueAuth(*Values) (*Values, error) {
	return nil, ErrUnexpected
}

func (*AIOServiceAccountToken) AuthSuccess(func()) {
	// TODO: start a timer or a file watcher for re-authentication. It is not
	// strictly necessary for the session client to function with MQ, but it
	// will prevent reconnections from occurring when the token expires.
}
