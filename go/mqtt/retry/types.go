// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retry

import "context"

type (
	// Task represents a function to retry. It should return a boolean
	// indicating whether a retry should occur on the given error.
	Task = func(context.Context) (shouldRetry bool, err error)

	// Policy is the retry policy for task execution.
	Policy interface {
		Start(ctx context.Context, name string, task Task) error
	}
)
