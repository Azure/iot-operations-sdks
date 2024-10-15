// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retry

import (
	"context"
	"log/slog"
	"math"
	"math/rand"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
)

// ExponentialBackoff implements a retry policy with exponential backoff and
// optional jitter.
type ExponentialBackoff struct {
	// MaxAttempts sets the maximum number of attempts. The default value of 0
	// indicates unlimited attempts; setting this to 1 will disable retries.
	MaxAttempts uint64

	// MinInterval is the maximum interval between retries (before jitter).
	// Will be set to a default of 1/8s if unspecified.
	MinInterval time.Duration

	// MaxInterval is the maximum interval between retries (before jitter).
	// Will be set to a default of 30s if unspecified.
	MaxInterval time.Duration

	// Timeout is the total timeout for all retries.
	Timeout time.Duration

	// NoJitter removes the default jitter.
	NoJitter bool

	// Logger provides a logger which will be used to log retry attempts and
	// results.
	Logger *slog.Logger
}

// Start initiates the retry executions.
func (e *ExponentialBackoff) Start(
	ctx context.Context,
	name string,
	task Task,
) error {
	// Create a context with timeout if specified.
	if e.Timeout > 0 {
		var cancel context.CancelFunc
		ctx, cancel = context.WithTimeout(ctx, e.Timeout)
		defer cancel()
	}

	l := logger{log.Wrap(e.Logger)}

	for attempt := uint64(1); ; attempt++ {
		l.attempt(ctx, name, attempt)
		retry, err := task(ctx)
		if err == nil {
			l.complete(ctx, name, attempt, nil)
			return nil
		}

		interval := e.shouldRetry(ctx, attempt, retry)
		if interval == 0 {
			l.complete(ctx, name, attempt, err)
			return err
		}

		select {
		case <-wallclock.Instance.After(interval):
		case <-ctx.Done():
			l.complete(ctx, name, attempt, ctx.Err())
			return ctx.Err()
		}
	}
}

// Decide if we need to continue/start retrying the target operations based on
// the retry count and other conditions.
func (e *ExponentialBackoff) shouldRetry(
	ctx context.Context,
	attempt uint64,
	retry bool,
) time.Duration {
	switch {
	case !retry,
		attempt == e.MaxAttempts,
		ctx.Err() != nil:
		return 0
	}

	minInterval := e.MinInterval
	if minInterval == 0 {
		minInterval = time.Second / 8
	}

	maxInterval := e.MaxInterval
	if maxInterval == 0 {
		maxInterval = 30 * time.Second
	}

	// Calculate exponent and clamp to max exponent.
	factor := math.Pow(2, min(
		float64(attempt-1),
		math.Log2(float64(maxInterval)/float64(minInterval)),
	))
	if !e.NoJitter {
		factor = e.jitter(factor)
	}

	return time.Duration(factor * float64(minInterval))
}

// Add random jitter to the base time to avoid synchronicity in retry attempts.
// The jitter is between 95% and 105% of the base time.
func (*ExponentialBackoff) jitter(base float64) float64 {
	// #nosec G404
	j := rand.New(rand.NewSource(wallclock.Instance.Now().UnixNano())).Float64()
	return base * (.95 + .1*j)
}
