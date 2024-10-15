// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retrypolicy

import "time"

// Option defines the functional option type.
type ExponentialBackoffRetryPolicyOption func(*ExponentialBackoffRetryPolicy)

// WithMaxRetries sets the maximum number of retries.
func WithMaxRetries(
	maxRetries int,
) ExponentialBackoffRetryPolicyOption {
	return func(policy *ExponentialBackoffRetryPolicy) {
		policy.maxRetries = &maxRetries
	}
}

// WithMaxInterval sets the maximum interval between retries.
func WithMaxInterval(
	maxInterval time.Duration,
) ExponentialBackoffRetryPolicyOption {
	return func(policy *ExponentialBackoffRetryPolicy) {
		policy.maxInterval = maxInterval
	}
}

// WithTimeout sets the timeout for the total retry.
func WithTimeout(
	timeout time.Duration,
) ExponentialBackoffRetryPolicyOption {
	return func(policy *ExponentialBackoffRetryPolicy) {
		policy.timeout = timeout
	}
}

// WithJitter sets whether to use jitter.
func WithJitter(
	withJitter bool,
) ExponentialBackoffRetryPolicyOption {
	return func(policy *ExponentialBackoffRetryPolicy) {
		policy.withJitter = withJitter
	}
}
