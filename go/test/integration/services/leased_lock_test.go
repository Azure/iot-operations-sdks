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

type leasedLockTest struct {
	*stateStoreTest
	name string
	lock *leasedlock.Lock[string, string]
}

func newLeasedLockTest(
	ctx context.Context,
	t *testing.T,
	name string,
) *leasedLockTest {
	test := &leasedLockTest{}
	test.stateStoreTest = newStateStoreTest(ctx, t)
	test.name = name
	test.lock = leasedlock.New(test.client, name)
	return test
}

func TestFencing(t *testing.T) {
	ctx := context.Background()
	test := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test.done()

	badFT, err := app.GetHLC()
	require.NoError(t, err)

	holder, err := test.lock.Holder(ctx)
	require.NoError(t, err)
	require.Empty(t, holder)

	ft, err := test.lock.TryAcquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.False(t, ft.IsZero())

	holder, err = test.lock.Holder(ctx)
	require.NoError(t, err)
	require.Equal(t, test.client.ID(), holder)

	test.set(ctx, t, true, uuid.NewString(), statestore.WithFencingToken(ft))

	_, err = test.client.Set(ctx, test.key, uuid.NewString(),
		statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	_, err = test.client.Del(ctx, test.key, statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	test.del(ctx, t, 1, statestore.WithFencingToken(ft))

	ok, err := test.lock.Release(ctx)
	require.NoError(t, err)
	require.True(t, ok)

	holder, err = test.lock.Holder(ctx)
	require.NoError(t, err)
	require.Empty(t, holder)
}

func TestEdit(t *testing.T) {
	ctx := context.Background()
	test := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test.done()

	initialValue := "someInitialValue"
	updatedValue := "someUpdatedValue"

	test.set(ctx, t, true, initialValue)
	require.NoError(t, test.lock.Edit(ctx, test.key, 10*time.Second,
		func(_ context.Context, val string) (string, error) {
			require.Equal(t, initialValue, val)
			return updatedValue, nil
		},
	))
	test.get(ctx, t, updatedValue)
}

func TestFencingWithSessionID(t *testing.T) {
	ctx := context.Background()
	test := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test.done()

	badFT, err := app.GetHLC()
	require.NoError(t, err)

	sessionID := uuid.NewString()

	ft, err := test.lock.TryAcquire(ctx, 10*time.Second,
		leasedlock.WithSessionID(sessionID))
	require.NoError(t, err)
	require.False(t, ft.IsZero())

	test.set(ctx, t, true, uuid.NewString(), statestore.WithFencingToken(ft))

	_, err = test.client.Set(ctx, test.key, uuid.NewString(),
		statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	_, err = test.client.Del(ctx, test.key, statestore.WithFencingToken(badFT))
	require.Equal(t, errors.FencingTokenLowerVersion, err)

	test.del(ctx, t, 1, statestore.WithFencingToken(ft))

	ok, err := test.lock.Release(ctx, leasedlock.WithSessionID(sessionID))
	require.NoError(t, err)
	require.True(t, ok)
}

func TestProactivelyReacquiringALock(t *testing.T) {
	ctx := context.Background()
	test := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test.done()

	oldFT, err := test.lock.TryAcquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	newFT, err := test.lock.TryAcquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.False(t, newFT.IsZero())

	require.Equal(t, -1, oldFT.Compare(newFT))
}

func TestAcquireLockWhenLockIsUnavailable(t *testing.T) {
	ctx := context.Background()
	test1 := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLeasedLockTest(ctx, t, test1.name)
	defer test2.done()

	oldFT, err := test1.lock.TryAcquire(ctx, 2*time.Second)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	newFT, err := test2.lock.Acquire(ctx, time.Second)
	require.NoError(t, err)
	require.False(t, newFT.IsZero())

	require.Equal(t, -1, oldFT.Compare(newFT))
}

func TestEditWhenLockIsUnavailable(t *testing.T) {
	ctx := context.Background()
	test1 := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLeasedLockTest(ctx, t, test1.name)
	defer test2.done()

	oldFT, err := test1.lock.TryAcquire(ctx, 2*time.Second)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	updatedValue := "someUpdatedValue"

	require.NoError(t, test2.lock.Edit(ctx, test1.key, time.Second,
		func(_ context.Context, val string) (string, error) {
			require.Empty(t, val)
			return updatedValue, nil
		},
	))
	test1.get(ctx, t, updatedValue)
}

func TestEditDoesNotUpdateValueIfLockNotAcquired(t *testing.T) {
	ctx := context.Background()
	test1 := newLeasedLockTest(ctx, t, uuid.NewString())
	defer test1.done()
	test2 := newLeasedLockTest(ctx, t, test1.name)
	defer test2.done()

	oldFT, err := test1.lock.TryAcquire(ctx, 10*time.Second)
	require.NoError(t, err)
	require.False(t, oldFT.IsZero())

	initialValue := "someInitialValue"
	updatedValue := "someUpdatedValue"

	test1.set(ctx, t, true, initialValue)
	require.Equal(t, test2.lock.Edit(ctx, test1.key, time.Second,
		func(context.Context, string) (string, error) {
			return updatedValue, nil
		},
		leasedlock.WithTimeout(time.Second),
	), context.DeadlineExceeded)
	test1.get(ctx, t, initialValue)
}
