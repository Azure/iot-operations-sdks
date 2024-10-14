package retrypolicy

import (
	"context"
	"fmt"
	"math"
	"math/rand"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
)

const (
	// Start with an exponent of 1 for the delay calculation,
	// which begins too low and takes too long to exceed 1 second.
	// Adding 6 to the retry count starts at 2^7 = 128 milliseconds,
	// exceeding 1 second on the 4th retry.
	minExponent = 6

	// Avoid integer overflow by clamping the maximum delay.
	// The maximum exponent value is set to 32.
	maxExponent = 32
)

// ExponentialBackoffRetry implements a retry policy
// with exponential backoff and optional jitter.
type ExponentialBackoffRetryPolicy struct {
	// If maxRetries is nil, we start indefinitely retry.
	maxRetries  *int
	maxInterval time.Duration
	// If timeout is 0, we start indefinitely retry.
	timeout    time.Duration
	withJitter bool
}

// NewExponentialBackoffRetryPolicy creates
// a new ExponentialBackoffRetryPolicy with the given options.
func NewExponentialBackoffRetryPolicy(
	opts ...ExponentialBackoffRetryPolicyOption,
) *ExponentialBackoffRetryPolicy {
	exponentialBackoffRetryPolicy := &ExponentialBackoffRetryPolicy{
		maxRetries:  nil, // Set indefinitely retry by default
		maxInterval: defaultMaxInterval,
		timeout:     0, // Set indefinitely retry by default
		withJitter:  defaultWithJitter,
	}

	for _, opt := range opts {
		opt(exponentialBackoffRetryPolicy)
	}

	return exponentialBackoffRetryPolicy
}

// Start initiates the retry executions.
func (e *ExponentialBackoffRetryPolicy) Start(
	ctx context.Context,
	log func(msg string, args ...any),
	task Task,
) error {
	var retryCtx context.Context
	var cancel context.CancelFunc

	// Create a context with timeout if specified
	if e.timeout != 0 {
		retryCtx, cancel = context.WithTimeout(ctx, e.timeout)
		defer cancel()
	} else {
		retryCtx = ctx
	}

	for try := 0; ; try++ {
		log(fmt.Sprintf(
			"retry: executing %s on attempt %d",
			task.Name,
			try+1,
		))
		err := task.Exec(retryCtx)
		if err == nil {
			e.status(log, task.Name, try, nil)
			return nil
		}

		interval := e.shouldRetry(retryCtx, try, task.Cond(err))
		if interval == 0 {
			e.status(log, task.Name, try, err)
			return err
		}

		select {
		case <-wallclock.Instance.After(interval):
		case <-retryCtx.Done():
			e.status(log, task.Name, try, retryCtx.Err())
			return retryCtx.Err()
		}
	}
}

// shouldRetry decides if we need to continue/start
// retrying the target operations
// based on retry count and other conditions.
func (e *ExponentialBackoffRetryPolicy) shouldRetry(
	ctx context.Context,
	retries int,
	cond bool,
) time.Duration {
	if e.maxRetries != nil &&
		(*e.maxRetries <= 0 ||
			retries >= *e.maxRetries-1 ||
			!cond ||
			ctx.Err() != nil) {
		return 0
	}

	// Calculate exponent and clamp to max exponent
	exp := retries + minExponent
	if exp > maxExponent {
		exp = maxExponent
	}
	expIntervalMs := math.Pow(2.0, float64(exp))
	clampedMs := math.Min(expIntervalMs, float64(e.maxInterval.Milliseconds()))
	interval := time.Duration(clampedMs) * time.Millisecond

	if e.withJitter {
		interval = e.jitter(clampedMs)
	}

	return interval
}

// status returns the result of each retry.
func (*ExponentialBackoffRetryPolicy) status(
	log func(msg string, args ...any),
	task string,
	try int,
	err error,
) {
	if err != nil {
		log(fmt.Sprintf(
			"retry: %s failed after %d attempt(s): %v",
			task,
			try+1,
			err,
		))
	} else {
		log(fmt.Sprintf(
			"retry: %s succeeded after %d attempt(s)",
			task,
			try+1,
		))
	}
}

// jitter adds a random jitter to the base time to avoid synchronicity
// in retry attempts.
// The jitter is between 95% and 105% of the base time.
func (*ExponentialBackoffRetryPolicy) jitter(base float64) time.Duration {
	// Generate a random jitter value between 0 and 10
	seed := wallclock.Instance.Now().UnixNano()
	// #nosec G404
	r := rand.New(rand.NewSource(seed))
	j := r.Intn(11)

	// Calculate jitter percentage by adding 95 to the random value,
	// resulting in a range of 95 to 105
	percent := j + 95

	// Apply the jitter percentage to the base time
	jitter := base * float64(percent) / 100.0

	return time.Duration(jitter) * time.Millisecond
}
