// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import "context"

// Background is an abstraction the concept of a long-running backround state,
// which other contexts may need to tie to. It's meant to isolate storing a
// context, which is typically a Go antipattern but is useful for tracking.
type Background struct {
	ctx    context.Context
	cancel context.CancelCauseFunc
	err    error
}

func NewBackground(err error) (context.Context, *Background) {
	b := &Background{err: err}
	b.ctx, b.cancel = context.WithCancelCause(context.Background())
	return b.ctx, b
}

// https://pkg.go.dev/context#example-AfterFunc-Merge
func (b *Background) Follow(
	ctx context.Context,
) (context.Context, context.CancelFunc) {
	c, cancel := context.WithCancelCause(ctx)
	stop := context.AfterFunc(b.ctx, func() {
		cancel(context.Cause(b.ctx))
	})
	return c, func() {
		stop()
		cancel(context.Canceled)
	}
}

func (b *Background) Close() {
	b.cancel(b.err)
}

func (b *Background) Done() <-chan struct{} {
	return b.ctx.Done()
}
