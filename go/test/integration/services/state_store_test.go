// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package services

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
)

type stateStoreTest struct {
	mqtt   *mqtt.SessionClient
	client *statestore.Client[string, string]
	key    string
}

func newStateStoreTest(ctx context.Context, t *testing.T) *stateStoreTest {
	test := &stateStoreTest{}
	var err error

	test.mqtt, err = mqtt.NewSessionClient(
		mqtt.TCPConnection("localhost", 1883),
	)
	require.NoError(t, err)

	test.client, err = statestore.New[string, string](test.mqtt)
	require.NoError(t, err)

	require.NoError(t, test.mqtt.Start())
	require.NoError(t, test.client.Start(ctx))

	test.key = uuid.NewString()

	return test
}

func (test *stateStoreTest) get(
	ctx context.Context,
	t *testing.T,
	exp string,
	opts ...statestore.GetOption,
) {
	get, err := test.client.Get(ctx, test.key, opts...)
	require.NoError(t, err)
	require.Equal(t, exp, get.Value)
}

func (test *stateStoreTest) zero(
	ctx context.Context,
	t *testing.T,
	exp bool,
	opts ...statestore.GetOption,
) {
	get, err := test.client.Get(ctx, test.key, opts...)
	require.NoError(t, err)
	require.Empty(t, get.Value)
	require.Equal(t, exp, get.Version.IsZero())
}

func (test *stateStoreTest) set(
	ctx context.Context,
	t *testing.T,
	exp bool,
	val string,
	opts ...statestore.SetOption,
) {
	set, err := test.client.Set(ctx, test.key, val, opts...)
	require.NoError(t, err)
	require.Equal(t, exp, set.Value)
}

func (test *stateStoreTest) del(
	ctx context.Context,
	t *testing.T,
	exp int,
	opts ...statestore.DelOption,
) {
	del, err := test.client.Del(ctx, test.key, opts...)
	require.NoError(t, err)
	require.Equal(t, exp, del.Value)
}

func (test *stateStoreTest) vdel(
	ctx context.Context,
	t *testing.T,
	exp int,
	val string,
	opts ...statestore.VDelOption,
) {
	del, err := test.client.VDel(ctx, test.key, val, opts...)
	require.NoError(t, err)
	require.Equal(t, exp, del.Value)
}

func (test *stateStoreTest) done() {
	test.client.Close()
	_ = test.mqtt.Stop()
}

func TestStateStoreObjectCRUD(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	val := uuid.NewString()
	test.set(ctx, t, true, val)
	test.get(ctx, t, val)
	test.del(ctx, t, 1)
	test.zero(ctx, t, true)
}

func TestStateStoreObjectExpiryTime(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	test.set(ctx, t, true, uuid.NewString(), statestore.WithExpiry(time.Second))
	time.Sleep(2 * time.Second)
	test.zero(ctx, t, true)
}

func TestStateStoreConditionalSet(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	val1 := uuid.NewString()
	val2 := uuid.NewString()
	test.set(ctx, t, true, val1, statestore.NotExists)
	test.set(ctx, t, false, val1, statestore.NotExists)
	test.set(ctx, t, false, val2, statestore.NotExistsOrEqual)
	test.set(ctx, t, true, val1, statestore.NotExistsOrEqual)
	test.del(ctx, t, 1)
}

func TestStateStoreConditionalDelete(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	val := uuid.NewString()
	test.set(ctx, t, true, val)
	test.vdel(ctx, t, -1, uuid.NewString())
	test.vdel(ctx, t, 1, val)
	test.vdel(ctx, t, 0, val)
}

func TestStateStoreObserveSingleKey(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	ch, done := test.client.Notify(test.key)
	defer done()
	require.NoError(t, test.client.KeyNotify(ctx, test.key))

	val := uuid.NewString()
	test.set(ctx, t, true, val)
	notify := <-ch
	require.Equal(t, test.key, notify.Key)
	require.Equal(t, "SET", notify.Operation)
	require.Equal(t, val, notify.Value)

	test.del(ctx, t, 1)
	notify = <-ch
	require.Equal(t, test.key, notify.Key)
	require.Equal(t, "DELETE", notify.Operation)
	require.Empty(t, notify.Value)
}

func TestStateStoreObserveSingleKeyThatExpires(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	ch, done := test.client.Notify(test.key)
	defer done()
	require.NoError(t, test.client.KeyNotify(ctx, test.key))

	val := uuid.NewString()
	test.set(ctx, t, true, val, statestore.WithExpiry(time.Second))
	notify := <-ch
	require.Equal(t, test.key, notify.Key)
	require.Equal(t, "SET", notify.Operation)
	require.Equal(t, val, notify.Value)

	notify = <-ch
	require.Equal(t, test.key, notify.Key)
	require.Equal(t, "DELETE", notify.Operation)
	require.Empty(t, notify.Value)
}

func TestStateStoreUnobserveSingleKey(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	ch, done := test.client.Notify(test.key)
	defer done()
	require.NoError(t, test.client.KeyNotify(ctx, test.key))

	test.set(ctx, t, true, uuid.NewString())
	test.del(ctx, t, 1)
	<-ch
	<-ch

	require.NoError(t, test.client.KeyNotifyStop(ctx, test.key))

	test.set(ctx, t, true, uuid.NewString())
	test.del(ctx, t, 1)

	select {
	case <-ch:
		t.Fail()
	case <-time.After(time.Second):
	}
}

func TestStateStoreEmptyValue(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	test.set(ctx, t, true, "")
	test.zero(ctx, t, false)
	test.del(ctx, t, 1)
}

func TestStateStoreEmptyKey(t *testing.T) {
	ctx := context.Background()
	test := newStateStoreTest(ctx, t)
	defer test.done()

	_, err := test.client.Set(ctx, "", "")
	require.ErrorIs(t, err, statestore.ErrArgument)
}
