// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"os"
)

type (
	// UsernameProvider is a function that returns an MQTT username and flag.
	// Note that if the returned flag is false, the returned username is
	// ignored.
	UsernameProvider func(context.Context) (string, bool, error)

	// PasswordProvider is a function that returns an MQTT password and flag.
	// Note that if the returned flag is false, the returned password is
	// ignored.
	PasswordProvider func(context.Context) ([]byte, bool, error)
)

// ConstantUsername is a UsernameProvider implementation that returns an
// unchanging username. This can be used if the username does not need to be
// updated between MQTT connections.
func ConstantUsername(username string) UsernameProvider {
	return func(context.Context) (string, bool, error) {
		return username, true, nil
	}
}

// ConstantPassword is a PasswordProvider implementation that returns an
// unchanging password. This can be used if the password does not need to be
// updated between MQTT connections.
func ConstantPassword(password []byte) PasswordProvider {
	return func(context.Context) ([]byte, bool, error) {
		return password, true, nil
	}
}

// FilePassword is a PasswordProvider implementation that reads a password from
// a given filename for each MQTT connection.
func FilePassword(filename string) PasswordProvider {
	return func(context.Context) ([]byte, bool, error) {
		data, err := os.ReadFile(filename)
		if err != nil {
			return nil, false, err
		}
		return data, true, nil
	}
}
