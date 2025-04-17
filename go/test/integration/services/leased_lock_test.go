// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package services

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/services/leasedlock"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

type (
	leaseTest struct {
		*stateStoreTest
		lease *leasedlock.Lease[string, string]
	}

	lockTest struct {
		*stateStoreTest
		lock leasedlock.Lock[string, string]
	}
)

func newLeaseTest(
	ctx context.Context,
	t *testing.T,
	name string,
	opt ...leasedlock.Option,
) *leaseTest {
	test := &leaseTest{}
	test.stateStoreTest = newStateStoreTest(ctx, t)
	test.lease = leasedlock.NewLease(test.client, name, opt...)
	return test
}

func newLockTest(
	ctx context.Context,
	t *testing.T,
	name string,
	opt ...leasedlock.Option,
) *lockTest {
	test := &lockTest{}
	test.stateStoreTest = newStateStoreTest(ctx, t)
	test.lock = leasedlock.NewLock(test.client, name, opt...)
	return test
}

func TestFencing(t *testing.T) {
	ctx := context.Background()
	test := newLeaseTest(ctx, t, uuid.NewString())
	defer test.done()

	badFT, err := app.GetHLC()
	require.NoError(t, err)

	holder, held, err := test.lease.Holder(ctx)
	require.NoError(t, err)
	require.False(t, held)
	require.Empty(t, holder)

	ok, err := test.lease.Acquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.True(t, ok)

	ft, err := test.lease.Token(ctx)
	require.NoError(t, err)
	require.False(t, ft.IsZero())

	holder, held, err = test.lease.Holder(ctx)
	require.NoError(t, err)
	require.True(t, held)
	require.Equal(t, test.client.ID(), holder)

	test.set(ctx, t, true, uuid.NewString(), statestore.WithFencingToken(ft))

	_, err = test.client.Set(ctx, test.key, uuid.NewString(),
		statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	_, err = test.client.Del(ctx, test.key, statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	test.del(ctx, t, 1, statestore.WithFencingToken(ft))

	err = test.lease.Release(ctx)
	require.NoError(t, err)

	holder, held, err = test.lease.Holder(ctx)
	require.NoError(t, err)
	require.False(t, held)
	require.Empty(t, holder)
}

func TestEdit(t *testing.T) {
	ctx := context.Background()
	test := newLockTest(ctx, t, uuid.NewString())
	defer test.done()

	initialValue := "someInitialValue"
	updatedValue := "someUpdatedValue"

	test.set(ctx, t, true, initialValue)
	require.NoError(t, test.lock.Edit(ctx, test.key, 10*time.Second,
		func(_ context.Context, val string, ex bool) (string, bool, error) {
			require.Equal(t, initialValue, val)
			require.True(t, ex)
			return updatedValue, true, nil
		},
	))
	test.get(ctx, t, updatedValue)
}

func TestFencingWithSessionID(t *testing.T) {
	ctx := context.Background()
	test := newLeaseTest(ctx, t, uuid.NewString(),
		leasedlock.WithSessionID(uuid.NewString()),
	)
	defer test.done()

	badFT, err := app.GetHLC()
	require.NoError(t, err)

	ok, err := test.lease.Acquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.True(t, ok)

	ft, err := test.lease.Token(ctx)
	require.NoError(t, err)
	require.False(t, ft.IsZero())

	test.set(ctx, t, true, uuid.NewString(), statestore.WithFencingToken(ft))

	_, err = test.client.Set(ctx, test.key, uuid.NewString(),
		statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	_, err = test.client.Del(ctx, test.key, statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	test.del(ctx, t, 1, statestore.WithFencingToken(ft))

	err = test.lease.Release(ctx)
	require.NoError(t, err)
}

func TestProactivelyReacquiringALease(t *testing.T) {
	ctx := context.Background()
	test := newLeaseTest(ctx, t, uuid.NewString())
	defer test.done()

	ok, err := test.lease.Acquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.True(t, ok)

	oldFT, err := test.lease.Token(ctx)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	ok, err = test.lease.Acquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.True(t, ok)

	newFT, err := test.lease.Token(ctx)
	require.NoError(t, err)
	require.False(t, newFT.IsZero())

	require.Equal(t, -1, oldFT.Compare(newFT))
}

func TestAcquireLockWhenLockIsUnavailable(t *testing.T) {
	ctx := context.Background()
	test1 := newLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLockTest(ctx, t, test1.lock.Name)
	defer test2.done()

	err := test1.lock.Lock(ctx, 2*time.Second)
	require.NoError(t, err)

	oldFT, err := test1.lock.Token(ctx)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	err = test2.lock.Lock(ctx, time.Second)
	require.NoError(t, err)

	newFT, err := test2.lock.Token(ctx)
	require.NoError(t, err)
	require.False(t, newFT.IsZero())

	require.Equal(t, -1, oldFT.Compare(newFT))
}

func TestEditWhenLockIsUnavailable(t *testing.T) {
	ctx := context.Background()
	test1 := newLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLockTest(ctx, t, test1.lock.Name)
	defer test2.done()

	err := test1.lock.Lock(ctx, 2*time.Second)
	require.NoError(t, err)

	oldFT, err := test1.lock.Token(ctx)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	updatedValue := "someUpdatedValue"

	require.NoError(t, test2.lock.Edit(ctx, test1.key, time.Second,
		func(_ context.Context, val string, ex bool) (string, bool, error) {
			require.Empty(t, val)
			require.False(t, ex)
			return updatedValue, true, nil
		},
	))
	test1.get(ctx, t, updatedValue)
}

func TestEditDoesNotUpdateValueIfLockNotAcquired(t *testing.T) {
	ctx := context.Background()
	test1 := newLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLockTest(ctx, t, test1.lock.Name)
	defer test2.done()

	err := test1.lock.Lock(ctx, 10*time.Second)
	require.NoError(t, err)

	oldFT, err := test1.lock.Token(ctx)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	initialValue := "someInitialValue"
	updatedValue := "someUpdatedValue"

	test1.set(ctx, t, true, initialValue)
	require.Equal(t, test2.lock.Edit(ctx, test1.key, time.Second,
		func(context.Context, string, bool) (string, bool, error) {
			return updatedValue, true, nil
		},
		leasedlock.WithTimeout(time.Second),
	), context.DeadlineExceeded)
	test1.get(ctx, t, initialValue)
}
