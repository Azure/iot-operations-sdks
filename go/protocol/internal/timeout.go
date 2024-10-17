// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"context"
	"fmt"
	"math"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Function to apply an optional timeout.
type Timeout = func(context.Context) (context.Context, context.CancelFunc)

// Apply an optional context timeout. Use for WithExecutionTimeout.
func NewTimeout(to time.Duration, kind errors.Kind, s string) (Timeout, error) {
	switch {
	case to < 0:
		return nil, &errors.Error{
			Message:       "timeout cannot be negative",
			Kind:          kind,
			PropertyName:  "Timeout",
			PropertyValue: to,
		}

	case to.Seconds() > math.MaxUint32:
		return nil, &errors.Error{
			Message:       "timeout too large",
			Kind:          kind,
			PropertyName:  "Timeout",
			PropertyValue: to,
		}

	case to == 0:
		return context.WithCancel, nil

	default:
		return func(ctx context.Context) (context.Context, context.CancelFunc) {
			return wallclock.Instance.WithTimeoutCause(ctx, to, &errors.Error{
				Message:      fmt.Sprintf("%s timed out", s),
				Kind:         errors.Timeout,
				TimeoutName:  "Timeout",
				TimeoutValue: to,
			})
		}, nil
	}
}
