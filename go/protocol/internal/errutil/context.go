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

// If the context was cancelled with a cause, it's either an error we've
// provided (already a protocol error), an error the user provided from a
// parent context (which should be respected as-is), or a special case we
// need to respond to. In any of these cases, we return the error unwrapped.
func normalize(err error, msg string, cause bool) error {
	if e, ok := err.(*errors.Client); ok {
		return e
	}

	switch {
	case err == nil:
		return nil

	case os.IsTimeout(err), stderr.Is(err, context.DeadlineExceeded):
		return &errors.Client{
			Base: errors.Base{
				Message: fmt.Sprintf("%s timed out", msg),
				Kind:    errors.Timeout,
			},
		}
	case stderr.Is(err, context.Canceled):
		return &errors.Client{
			Base: errors.Base{
				Message: fmt.Sprintf("%s cancelled", msg),
				Kind:    errors.Cancellation,
			},
		}

	default:
		if cause {
			return err
		}
		return &errors.Client{
			Base: errors.Base{
				Message:     fmt.Sprintf("%s error: %s", msg, err.Error()),
				Kind:        errors.UnknownError,
				NestedError: err,
			},
		}
	}
}

// Normalize well-known errors into protocol errors.
func Normalize(err error, msg string) error {
	return normalize(err, msg, false)
}

// Context extracts the timeout or cancellation error from a context.
func Context(ctx context.Context, msg string) error {
	return normalize(context.Cause(ctx), msg, true)
}
