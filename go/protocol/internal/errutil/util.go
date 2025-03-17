// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errutil

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/google/uuid"
)

type noReturn struct{ error }

// Indicate that this error cannot be returned over RPC.
func NoReturn(err error) error {
	return noReturn{err}
}

// Get whether this error is returnable, and the underlying error.
func IsNoReturn(err error) (bool, error) {
	if e, ok := err.(noReturn); ok {
		return true, e.error
	}
	return false, err
}

// Prepare the error for returning, removing any no-return flags (since this is
// used outside of the RPC context) and applying the shallow flag if possible.
func Return(err error, logger log.Logger, shallow bool) error {
	if e, ok := err.(noReturn); ok {
		err = e.error
	}
	if e, ok := err.(*errors.Client); ok {
		e.Shallow = shallow
	}
	if err != nil {
		logger.Warn(context.Background(), err)
	}
	return err
}

// Validate that a collection of arguments are not nil.
func ValidateNonNil(args map[string]any) error {
	for k, v := range args {
		if v == nil {
			return &errors.Client{
				Message: "argument is nil",
				Kind: errors.ConfigurationInvalid{
					PropertyName: k,
				},
			}
		}
	}
	return nil
}

// Generate UUID with protocol error.
func NewUUID() (string, error) {
	correlation, err := uuid.NewV7()
	if err != nil {
		return "", &errors.Client{
			Message: err.Error(),
			Kind:    errors.UnknownError{},
			Nested:  err,
		}
	}
	return correlation.String(), nil
}
