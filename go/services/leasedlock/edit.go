// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

// Edit provides a callback to edit a value under protection of a lock. Given
// the current value when the lock is acquired and whether that value was
// present, it should return the updated value and whether the new value should
// be set (true) or deleted (false).
type Edit[V Bytes] = func(context.Context, V, bool) (V, bool, error)

// Edit a key under the protection of this lock.
func (l *Lock[K, V]) Edit(
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

func (l *Lock[K, V]) edit(
	ctx context.Context,
	key K,
	duration time.Duration,
	edit Edit[V],
	opts *Options,
) (bool, error) {
	err := l.Acquire(ctx, duration, opts)
	if err != nil {
		return false, err
	}

	//nolint:errcheck // TODO: Is there anything useful to do if this fails?
	defer l.Release(ctx, opts)

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
