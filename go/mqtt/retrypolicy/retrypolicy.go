package retrypolicy

import (
	"context"
)

type (
	// Task represents a function to retry.
	Task struct {
		Name string                      // Task name
		Exec func(context.Context) error // Target function
		Cond func(error) bool            // Retry condition based on the error
	}

	// RetryPolicy is the retry strategy for Task execution.
	RetryPolicy interface {
		Start(
			ctx context.Context,
			log func(msg string, args ...any),
			task Task,
		) error
	}
)
