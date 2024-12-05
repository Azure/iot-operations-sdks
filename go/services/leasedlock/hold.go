// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"context"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

// Hold represents an active hold on the lock that will automatically renew.
type Hold[K, V Bytes] struct {
	close func()
	token hlc.HybridLogicalClock
	error error
	ch    chan struct{}
	mu    sync.RWMutex
}

// Hold returns a hold on the lock which will acquire the lock and then and
// automatically renew at the given interval.
func (l *Lock[K, V]) Hold(
	duration time.Duration,
	interval time.Duration,
	opt ...Option,
) *Hold[K, V] {
	// The hold starts in a locked state to make sure any calls to Token will
	// wait for the goroutine to start up.
	hold := &Hold[K, V]{ch: make(chan struct{})}

	var ctx context.Context
	ctx, hold.close = context.WithCancel(context.Background())
	go func() {
		for {
			// Token holds the read lock *and* waits for the channel, making
			// this update safe even though we're not under the write lock.
			hold.token, hold.error = l.Acquire(ctx, duration, opt...)
			close(hold.ch)

			if hold.error != nil {
				return
			}

			select {
			case <-time.After(interval):
				// Hold the actual write lock only long enough to update the
				// channel. This allows calls to Token to use the channel to
				// wait on the renew, which allows them to cancel themselves.
				hold.mu.Lock()
				hold.ch = make(chan struct{})
				hold.mu.Unlock()

			case <-ctx.Done():
				hold.mu.Lock()
				hold.token, hold.error = hlc.HybridLogicalClock{}, ctx.Err()
				hold.mu.Unlock()
				return
			}
		}
	}()

	return hold
}

// Token returns the current fencing token value, or the error that caused this
// hold to terminate. Note that this function will block if the hold is in the
// process of renewing the lock.
func (h *Hold[K, V]) Token(
	ctx context.Context,
) (hlc.HybridLogicalClock, error) {
	h.mu.RLock()
	defer h.mu.RUnlock()

	select {
	case <-h.ch:
		return h.token, h.error

	case <-ctx.Done():
		return hlc.HybridLogicalClock{}, ctx.Err()
	}
}

// Close stops renewing the lock.
func (h *Hold[K, V]) Close() {
	h.close()
}
