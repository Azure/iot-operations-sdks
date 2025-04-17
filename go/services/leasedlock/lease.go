// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"context"
	"errors"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

type (
	// Bytes represents generic byte data.
	Bytes = statestore.Bytes

	// Lease provides a distributed lease based on an underlying state store.
	Lease[K, V Bytes] struct {
		Name      K
		SessionID string

		client *statestore.Client[K, V]
		result result

		done context.CancelFunc
		mu   much
	}

	// Change represents an observed change in the lease holder.
	Change struct {
		Held   bool
		Holder string
	}

	// Result of the previous lease attempt, with its own lock for concurrency.
	result struct {
		token hlc.HybridLogicalClock
		error error
		mu    much
	}
)

var (
	// ErrNoLease is used in absence of other errors to indicate that the lease
	// has not been acquired.
	ErrNoLease = errors.New("lease not acquired")

	// ErrRenewing indicates that renew was specified on a lease that is already
	// renewing.
	ErrRenewing = errors.New("lease already renewing")
)

// NewLease creates a new distributed lease from an underlying state store
// client and a lease name.
func NewLease[K, V Bytes](
	client *statestore.Client[K, V],
	name K,
	opt ...Option,
) *Lease[K, V] {
	var opts Options
	opts.Apply(opt)

	return &Lease[K, V]{
		Name:      name,
		SessionID: opts.SessionID,

		client: client,
		result: result{
			error: ErrNoLease,
			mu:    make(chan struct{}, 1),
		},
		mu: make(chan struct{}, 1),
	}
}

func (l *Lease[K, V]) id(opts *Options) V {
	switch {
	case opts.SessionID != "":
		return V(l.client.ID() + ":" + opts.SessionID)
	case l.SessionID != "":
		return V(l.client.ID() + ":" + l.SessionID)
	default:
		return V(l.client.ID())
	}
}

// Token returns the current fencing token value or the error that caused the
// lease to fail. Note that this function will block if the lease is currently
// renewing and can be cancelled using its context.
func (l *Lease[K, V]) Token(
	ctx context.Context,
) (hlc.HybridLogicalClock, error) {
	if err := l.result.mu.Lock(ctx); err != nil {
		return hlc.HybridLogicalClock{}, err
	}
	defer l.result.mu.Unlock()

	return l.result.token, l.result.error
}

// Acquire performs a single attempt to acquire the lease, returning whether it
// was successful. If the lease was already held by another client, this will
// return false with no error.
func (l *Lease[K, V]) Acquire(
	ctx context.Context,
	duration time.Duration,
	opt ...Option,
) (bool, error) {
	var opts Options
	opts.Apply(opt)

	if err := l.mu.Lock(ctx); err != nil {
		return false, err
	}
	defer l.mu.Unlock()

	// Error on duplicate renews so we don't start up conflicting goroutines.
	if opts.Renew > 0 && l.done != nil {
		return false, ErrRenewing
	}

	ok, err := l.try(ctx, duration, &opts)
	if !ok || err != nil {
		return ok, err
	}

	// If specified, renew until an attempt fails or the lease is released.
	if opts.Renew > 0 {
		var ctx context.Context
		ctx, l.done = context.WithCancel(context.Background())
		go func() {
			for {
				select {
				case <-time.After(opts.Renew):
					ok, _ := l.try(ctx, duration, &opts)
					if !ok {
						return
					}
				case <-ctx.Done():
				}
			}
		}()
	}

	return true, nil
}

func (l *Lease[K, V]) try(
	ctx context.Context,
	duration time.Duration,
	opts *Options,
) (bool, error) {
	if err := l.result.mu.Lock(ctx); err != nil {
		return false, err
	}
	defer l.result.mu.Unlock()

	res, err := l.client.Set(
		ctx,
		l.Name,
		l.id(opts),
		opts.set(),
		statestore.WithCondition(statestore.NotExistsOrEqual),
		statestore.WithExpiry(duration),
	)
	if err != nil {
		l.result.token, l.result.error = hlc.HybridLogicalClock{}, err
		return false, err
	}
	if !res.Value {
		l.result.token, l.result.error = hlc.HybridLogicalClock{}, ErrNoLease
		return false, nil
	}

	l.result.token, l.result.error = res.Version, nil
	return true, nil
}

// Release the lease.
func (l *Lease[K, V]) Release(
	ctx context.Context,
	opt ...Option,
) error {
	var opts Options
	opts.Apply(opt)

	// Stop any renew.
	if err := l.mu.Lock(ctx); err != nil {
		return err
	}
	defer l.mu.Unlock()

	if l.done != nil {
		l.done()
		l.done = nil
	}

	// Reset the token.
	if err := l.result.mu.Lock(ctx); err != nil {
		return err
	}
	defer l.result.mu.Unlock()

	l.result.token, l.result.error = hlc.HybridLogicalClock{}, ErrNoLease

	// Release the lease.
	_, err := l.client.VDel(ctx, l.Name, l.id(&opts), opts.vdel())
	return err
}

// Holder gets the current holder of the lease and an indicator of whether the
// lease is currently held.
func (l *Lease[K, V]) Holder(
	ctx context.Context,
	opt ...Option,
) (string, bool, error) {
	var opts Options
	opts.Apply(opt)

	res, err := l.client.Get(ctx, l.Name, opts.get())
	if err != nil {
		return "", false, err
	}
	return string(res.Value), !res.Version.IsZero(), nil
}

// ObserveStart initializes observation of lease holder changes. It should be
// paired with a call to ObserveStop.
func (l *Lease[K, V]) ObserveStart(ctx context.Context, opt ...Option) error {
	var opts Options
	opts.Apply(opt)

	return l.client.KeyNotify(ctx, l.Name, opts.keynotify())
}

// ObserveStop terminates observation of lease holder changes. It should only be
// called once per successfull call to ObserveStart (but may be retried in case
// of failure).
func (l *Lease[K, V]) ObserveStop(ctx context.Context, opt ...Option) error {
	var opts Options
	opts.Apply(opt)

	return l.client.KeyNotifyStop(ctx, l.Name, opts.keynotify())
}

// Observe requests a lease holder change notification channel for this lease.
// It returns the channel and a function to remove and close that channel. Note
// that ObserveStart must be called to actually start observing (though changes
// may be received on this channel if ObserveStart had already been called
// previously).
func (l *Lease[K, V]) Observe() (<-chan Change, func()) {
	obs := make(chan Change)
	kn, done := l.client.Notify(l.Name)

	// Spin up a simple translation of NOTIFY to Change. Calling done() will
	// close the kn channel, terminating this loop.
	go func() {
		defer close(obs)
		for n := range kn {
			obs <- Change{
				Held:   n.Operation != "DELETE",
				Holder: string(n.Value),
			}
		}
	}()

	return obs, done
}
