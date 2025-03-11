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

// Struct to apply an optional timeout.
type Timeout struct {
	time.Duration
	Name string
	Text string
}

func (to *Timeout) Validate(kind errors.Kind) error {
	switch {
	case to.Duration < 0:
		return &errors.Client{
			Base: errors.Base{
				Message:       "timeout cannot be negative",
				Kind:          kind,
				PropertyName:  "Timeout",
				PropertyValue: to,
			},
		}

	case to.Seconds() > math.MaxUint32:
		return &errors.Client{
			Base: errors.Base{
				Message:       "timeout too large",
				Kind:          kind,
				PropertyName:  "Timeout",
				PropertyValue: to,
			},
		}

	default:
		return nil
	}
}

func (to *Timeout) Context(
	ctx context.Context,
) (context.Context, context.CancelFunc) {
	if to.Duration == 0 {
		return context.WithCancel(ctx)
	}
	return wallclock.Instance.WithTimeoutCause(
		ctx,
		to.Duration,
		&errors.Remote{
			Base: errors.Base{
				Message:      fmt.Sprintf("%s timed out", to.Text),
				Kind:         errors.Timeout,
				TimeoutName:  to.Name,
				TimeoutValue: to.Duration,
			},
		},
	)
}

func (to *Timeout) MessageExpiry() uint32 {
	return uint32(math.Ceil(to.Seconds()))
}
