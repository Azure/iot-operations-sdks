package internal

import (
	"context"
	"fmt"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
)

// Function to apply an optional timeout.
type Timeout func(context.Context) (context.Context, context.CancelFunc)

// Apply an optional context timeout. Use for WithExecutionTimeout.
func NewExecutionTimeout(to time.Duration, s string) (Timeout, error) {
	switch {
	case to < 0:
		return nil, &errors.Error{
			Message:       "timeout cannot be negative",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "ExecutionTimeout",
			PropertyValue: to,
		}

	case to == 0:
		return context.WithCancel, nil

	default:
		return func(ctx context.Context) (context.Context, context.CancelFunc) {
			return wallclock.Instance.WithTimeoutCause(ctx, to, &errors.Error{
				Message:      fmt.Sprintf("%s timed out", s),
				Kind:         errors.Timeout,
				TimeoutName:  "ExecutionTimeout",
				TimeoutValue: to,
			})
		}, nil
	}
}

// Translate an MQTT message expiry into a timeout. Use for WithMessageExpiry.
func MessageExpiryTimeout(
	ctx context.Context,
	expiry uint32,
	s string,
) (context.Context, context.CancelFunc) {
	if expiry > 0 {
		to := time.Duration(expiry) * time.Second
		return wallclock.Instance.WithTimeoutCause(
			ctx,
			to,
			errutil.NoReturn(&errors.Error{
				Message: fmt.Sprintf(
					"message expired while processing %s",
					s,
				),
				Kind:         errors.Timeout,
				TimeoutName:  "MessageExpiry",
				TimeoutValue: to,
			}),
		)
	}
	return context.WithCancel(ctx)
}
