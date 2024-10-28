// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

type (
	// Bytes represents generic byte data.
	Bytes = statestore.Bytes

	// Lock provides a leased lock based on an underlying state store.
	Lock[K, V Bytes] struct {
		Name K

		client *statestore.Client[K, V]
	}
)

// New creates a new leased lock from an underlying state store client and a
// lock name.
func New[K, V Bytes](client *statestore.Client[K, V], name K) *Lock[K, V] {
	return &Lock[K, V]{name, client}
}

// TryAcquire performs a single attempt to acquire the lock, returning a nonzero
// fencing token if successful.
func (l *Lock[K, V]) TryAcquire(
	ctx context.Context,
	duration time.Duration,
	opt ...Option,
) (hlc.HybridLogicalClock, error) {
	var opts Options
	opts.Apply(opt)

	res, err := l.client.Set(
		ctx,
		l.Name,
		V(l.client.ID()),
		opts.set(),
		statestore.WithCondition(statestore.NotExistsOrEqual),
		statestore.WithExpiry(duration),
	)
	if err != nil || !res.Value {
		return hlc.HybridLogicalClock{}, err
	}
	return res.Version, nil
}

// Acquire the lock and return its fencing token. Note that cancelling the
// context passed to this method will prevent the lock notification from
// stopping; it is recommended to use WithTimeout instead.
func (l *Lock[K, V]) Acquire(
	ctx context.Context,
	duration time.Duration,
	opt ...Option,
) (hlc.HybridLogicalClock, error) {
	var opts Options
	opts.Apply(opt)

	// Register notification first so we don't miss a delete.
	if err := l.client.KeyNotify(ctx, l.Name, opts.keynotify()); err != nil {
		return hlc.HybridLogicalClock{}, err
	}

	//nolint:errcheck // TODO: Is there anything useful to do if this fails?
	defer l.client.KeyNotifyStop(ctx, l.Name, opts.keynotify())

	kn, done := l.client.Notify(l.Name)
	defer done()

	// Respect any requested timeout while waiting for the delete.
	if opts.Timeout > 0 {
		var cancel context.CancelFunc
		ctx, cancel = context.WithTimeout(ctx, opts.Timeout)
		defer cancel()
	}

	for {
		ft, err := l.TryAcquire(ctx, duration, opt...)
		if err != nil {
			return hlc.HybridLogicalClock{}, err
		}
		if !ft.IsZero() {
			return ft, nil
		}

		if err := waitForDelete(ctx, kn); err != nil {
			return hlc.HybridLogicalClock{}, err
		}
	}
}

func waitForDelete[K, V Bytes](
	ctx context.Context,
	kn <-chan statestore.Notify[K, V],
) error {
	for {
		select {
		case n := <-kn:
			if n.Operation == "DELETE" {
				return nil
			}
		case <-ctx.Done():
			return context.Cause(ctx)
		}
	}
}

// Release the lock. Returns a boolean indicating whether the lock was
// successfully released (e.g. whether the lock had been acquired).
func (l *Lock[K, V]) Release(
	ctx context.Context,
	opt ...Option,
) (bool, error) {
	var opts Options
	opts.Apply(opt)

	res, err := l.client.VDel(ctx, l.Name, V(l.client.ID()), opts.vdel())
	if err != nil {
		return false, err
	}
	return res.Value > 0, nil
}
