package internal

import "context"

// Manages sending values to handlers with a configured maximum currency (where
// 0 indicates unlimited concurrency). Returns a function to send a value to the
// handlers and a cleanup function.
func Concurrent[T any](
	concurrency uint,
	handler func(context.Context, T),
) (func(context.Context, T), func()) {
	type args struct {
		ctx context.Context
		val T
	}

	// For no maximum concurrency, spin up a goroutine for each message.
	if concurrency == 0 {
		return func(ctx context.Context, val T) {
			go handler(ctx, val)
		}, func() {}
	}

	// If a maximum concurrency was specified, spin up a number of goroutines
	// equal to that value to handle dispatched messages.
	dispatch := make(chan args)
	for i := uint(0); i < concurrency; i++ {
		go func() {
			for a := range dispatch {
				handler(a.ctx, a.val)
			}
		}()
	}

	// Send all arguments to the dispatcher channel (including the context
	// so that it controls the lifecycle of this handler invocation).
	return func(ctx context.Context, val T) {
		select {
		case dispatch <- args{ctx, val}:
		case <-ctx.Done():
		}
	}, func() { close(dispatch) }
}
