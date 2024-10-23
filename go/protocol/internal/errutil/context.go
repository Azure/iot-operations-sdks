// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errutil

import (
	"context"
	stderr "errors"
	"fmt"
	"os"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Normalize well-known errors into protocol errors.
func Normalize(err error, msg string) error {
	if e, ok := err.(*errors.Error); ok {
		return e
	}

	switch {
	case err == nil:
		return nil

	case os.IsTimeout(err), stderr.Is(err, context.DeadlineExceeded):
		return &errors.Error{
			Message: fmt.Sprintf("%s timed out", msg),
			Kind:    errors.Timeout,
		}

	case stderr.Is(err, context.Canceled):
		return &errors.Error{
			Message: fmt.Sprintf("%s cancelled", msg),
			Kind:    errors.Cancellation,
		}

	default:
		return &errors.Error{
			Message:     fmt.Sprintf("%s error: %s", msg, err.Error()),
			Kind:        errors.UnknownError,
			NestedError: err,
		}
	}
}

// Context extracts the timeout or cancellation error from a context.
func Context(ctx context.Context, msg string) error {
	// If the context was cancelled with a cause, it's either an error we've
	// provided (already a protocol error), an error the user provided from a
	// parent context (which should be respected as-is), or a special case we
	// need to respond to. In any of these cases, we return the error unwrapped.
	if err := context.Cause(ctx); err != nil {
		return err
	}
	return Normalize(ctx.Err(), msg)
}
