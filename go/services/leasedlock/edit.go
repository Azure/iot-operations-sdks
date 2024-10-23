// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

// Edit a key under the protection of this lock.
func (l *Lock[K, V]) Edit(
	ctx context.Context,
	key K,
	duration time.Duration,
	edit func(context.Context, V) (V, error),
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
	edit func(context.Context, V) (V, error),
	opts *Options,
) (bool, error) {
	ft, err := l.Acquire(ctx, duration, opts)
	if err != nil {
		return false, err
	}
	wft := statestore.WithFencingToken(ft)

	//nolint:errcheck // TODO: Is there anything useful to do if this fails?
	defer l.Release(ctx, opts)

	get, err := l.client.Get(ctx, key, opts.get())
	if err != nil {
		return false, err
	}

	upd, err := edit(ctx, get.Value)
	if err != nil {
		return false, err
	}

	if len(upd) == 0 {
		res, err := l.client.Del(ctx, key, opts.del(), wft)
		return err == nil && res.Value > 0, err
	}
	res, err := l.client.Set(ctx, key, upd, opts.set(), wft)
	return err == nil && res.Value, err
}
