// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"context"
	"sync"
)

// Background is an abstraction the concept of a long-running backround process,
// which contexts may need to tie to.
type Background struct {
	err   error
	done  chan struct{}
	close func()
}

func NewBackground(err error) *Background {
	done := make(chan struct{})
	return &Background{err, done, sync.OnceFunc(func() { close(done) })}
}

func (b *Background) With(
	ctx context.Context,
) (context.Context, context.CancelFunc) {
	c, cancel := context.WithCancelCause(ctx)
	go func() {
		select {
		case <-b.done:
			cancel(b.err)
		case <-c.Done():
		}
	}()
	return c, func() { cancel(context.Canceled) }
}

func (b *Background) Close() {
	b.close()
}

func (b *Background) Done() <-chan struct{} {
	return b.done
}
