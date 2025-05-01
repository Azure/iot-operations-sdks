// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package leasedlock

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

type (
	// Lock provides a distributed mutex-like lock based on an underlying state
	// store.
	Lock[K, V Bytes] struct{ *Lease[K, V] }

	// Edit provides a callback to edit a value under protection of a lock.
	// Given the current value when the lock is acquired and whether that value
	// was present, it should return the updated value and whether the new value
	// should be set (true) or deleted (false).
	Edit[V Bytes] = func(context.Context, V, bool) (V, bool, error)
)

// NewLock creates a new distributed lock from an underlying state store client
// and a lock name.
func NewLock[K, V Bytes](
	client *statestore.Client[K, V],
	name K,
	opt ...Option,
) Lock[K, V] {
	return Lock[K, V]{NewLease(client, name, opt...)}
}

// Lock the lock object, blocking until locked or the request fails. Note that
// cancelling the context passed to this method will prevent the underlying
// notification from stopping; it is recommended to use WithTimeout instead.
func (l Lock[K, V]) Lock(
	ctx context.Context,
	duration time.Duration,
	opt ...Option,
) error {
	var opts Options
	opts.Apply(opt)

	// Register notification first so we don't miss a delete.
	if err := l.client.KeyNotify(ctx, l.Name, opts.keynotify()); err != nil {
		return err
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
		ok, err := l.Acquire(ctx, duration, opt...)
		if err != nil {
			return err
		}
		if ok {
			return nil
		}

		if err := waitForDelete(ctx, kn); err != nil {
			return err
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

// Unlock the lock object.
func (l Lock[K, V]) Unlock(
	ctx context.Context,
	opt ...Option,
) error {
	return l.Release(ctx, opt...)
}

// Edit a key under the protection of this lock.
func (l Lock[K, V]) Edit(
	ctx context.Context,
	key K,
	duration time.Duration,
	edit Edit[V],
	opt ...Option,
) error {
	var opts Options
	opts.Apply(opt)

	var done bool
	var err error
	for err == nil && !done {
		done, err = l.edit(ctx, key, duration, edit, &opts)
	}
	return err
}

func (l Lock[K, V]) edit(
	ctx context.Context,
	key K,
	duration time.Duration,
	edit Edit[V],
	opts *Options,
) (bool, error) {
	err := l.Lock(ctx, duration, opts)
	if err != nil {
		return false, err
	}

	//nolint:errcheck // TODO: Is there anything useful to do if this fails?
	defer l.Unlock(ctx, opts)

	ft, err := l.Token(ctx)
	if err != nil {
		return false, err
	}
	wft := statestore.WithFencingToken(ft)

	get, err := l.client.Get(ctx, key, opts.get())
	if err != nil {
		return false, err
	}

	upd, set, err := edit(ctx, get.Value, !get.Version.IsZero())
	if err != nil {
		return false, err
	}

	if !set {
		res, err := l.client.Del(ctx, key, opts.del(), wft)
		return err == nil && res.Value > 0, err
	}
	res, err := l.client.Set(ctx, key, upd, opts.set(), wft)
	return err == nil && res.Value, err
}
