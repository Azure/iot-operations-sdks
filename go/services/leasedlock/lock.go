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

	// Lock provides a leased lock based on an underlying state store.
	Lock[K, V Bytes] struct {
		Name      K
		SessionID string

		client *statestore.Client[K, V]
		result result

		done context.CancelFunc
		mu   much
	}

	// Change represents an observed change in the lock holder.
	Change struct {
		Held   bool
		Holder string
	}

	// Result of the previous lock attempt, with its own lock for concurrency.
	result struct {
		token hlc.HybridLogicalClock
		error error
		mu    much
	}
)

var (
	// ErrNoLock is used in absence of other errors to indicate that the lock
	// has not been acquired.
	ErrNoLock = errors.New("lock not acquired")

	// ErrRenewing indicates that renew was specified on a lock that is already
	// renewing.
	ErrRenewing = errors.New("lock already renewing")
)

// New creates a new leased lock from an underlying state store client and a
// lock name.
func New[K, V Bytes](
	client *statestore.Client[K, V],
	name K,
	opt ...Option,
) *Lock[K, V] {
	var opts Options
	opts.Apply(opt)

	return &Lock[K, V]{
		Name:      name,
		SessionID: opts.SessionID,

		client: client,
		result: result{
			error: ErrNoLock,
			mu:    make(chan struct{}, 1),
		},
		mu: make(chan struct{}, 1),
	}
}

func (l *Lock[K, V]) id(opts *Options) V {
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
// lock to fail. Note that this function will block if the lock is currently
// renewing and can be cancelled using its context.
func (l *Lock[K, V]) Token(
	ctx context.Context,
) (hlc.HybridLogicalClock, error) {
	if err := l.result.mu.Lock(ctx); err != nil {
		return hlc.HybridLogicalClock{}, err
	}
	defer l.result.mu.Unlock()

	return l.result.token, l.result.error
}

// TryAcquire performs a single attempt to acquire the lock, returning whether
// it was successful. If the lock was already held by another client, this will
// return false with no error.
func (l *Lock[K, V]) TryAcquire(
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

	// If specified, renew until an attempt fails or the lock is released.
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

func (l *Lock[K, V]) try(
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
		l.result.token, l.result.error = hlc.HybridLogicalClock{}, ErrNoLock
		return false, nil
	}

	l.result.token, l.result.error = res.Version, nil
	return true, nil
}

// Acquire the lock, blocking until the lock is acquired or the request fails.
// Note that cancelling the context passed to this method will prevent the lock
// notification from stopping; it is recommended to use WithTimeout instead.
func (l *Lock[K, V]) Acquire(
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
		ok, err := l.TryAcquire(ctx, duration, opt...)
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

// Release the lock.
func (l *Lock[K, V]) Release(
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

	l.result.token, l.result.error = hlc.HybridLogicalClock{}, ErrNoLock

	// Release the lock.
	_, err := l.client.VDel(ctx, l.Name, l.id(&opts), opts.vdel())
	return err
}

// Holder gets the current holder of the lock and an indicator of whether the
// lock is currently held.
func (l *Lock[K, V]) Holder(
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

// ObserveStart initializes observation of lock holder changes. It should be
// paired with a call to ObserveStop.
func (l *Lock[K, V]) ObserveStart(ctx context.Context, opt ...Option) error {
	var opts Options
	opts.Apply(opt)

	return l.client.KeyNotify(ctx, l.Name, opts.keynotify())
}

// ObserveStop terminates observation of lock holder changes. It should only be
// called once per successfull call to ObserveStart (but may be retried in case
// of failure).
func (l *Lock[K, V]) ObserveStop(ctx context.Context, opt ...Option) error {
	var opts Options
	opts.Apply(opt)

	return l.client.KeyNotifyStop(ctx, l.Name, opts.keynotify())
}

// Observe requests a lock holder change notification channel for this lock. It
// returns the channel and a function to remove and close that channel. Note
// that ObserveStart must be called to actually start observing (though changes
// may be received on this channel if ObserveStart had already been called
// previously).
func (l *Lock[K, V]) Observe() (<-chan Change, func()) {
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
