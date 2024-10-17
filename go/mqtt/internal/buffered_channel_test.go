// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal_test

import (
	"sync"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/stretchr/testify/require"
)

func TestNewBufferChan(t *testing.T) {
	ch := internal.NewBufferChan[int](1)
	require.NotNil(t, ch)
	require.NotNil(t, ch.C)
	require.Equal(t, false, ch.Closed)
}

func TestBufferChan_Send(t *testing.T) {
	ch := internal.NewBufferChan[int](1)

	require.Equal(t, true, ch.Send(1))

	ch.Mu.Lock()
	ch.Closed = true
	ch.Mu.Unlock()

	require.Equal(t, false, ch.Send(2))
}

func TestBufferChan_Send_Blocking(t *testing.T) {
	ch := internal.NewBufferChan[int](1)
	defer ch.Close()

	require.Equal(t, true, ch.Send(1))
	require.Equal(t, false, ch.Send(2))
}

func TestBufferChan_Close(t *testing.T) {
	ch := internal.NewBufferChan[int](1)

	require.Equal(t, false, ch.Closed)

	ch.Close()

	require.Equal(t, true, ch.Closed)
	require.Nil(t, ch.C)
}

func TestBufferChan_Async_Send(t *testing.T) {
	ch := internal.NewBufferChan[int](100)
	defer ch.Close()

	var wg sync.WaitGroup
	wg.Add(2)

	go func() {
		defer wg.Done()
		for i := 0; i < 100; i++ {
			require.Equal(t, true, ch.Send(i))
		}
	}()

	time.Sleep(time.Second)

	go func() {
		defer wg.Done()
		for i := 0; i < 100; i++ {
			require.Equal(t, false, ch.Send(i))
		}
	}()

	wg.Wait()

	ch.Mu.Lock()
	defer ch.Mu.Unlock()
	require.Equal(t, false, ch.Closed)
}

func TestBufferChan_SendAfterClose(t *testing.T) {
	ch := internal.NewBufferChan[int](1)
	ch.Close()

	require.Equal(t, false, ch.Send(1))
}

func TestBufferChan_CloseTwice(t *testing.T) {
	ch := internal.NewBufferChan[int](1)

	ch.Close()
	ch.Close()

	require.Nil(t, ch.C)
	require.Equal(t, true, ch.Closed)
}

func TestBufferChan_SendAndClose(t *testing.T) {
	ch := internal.NewBufferChan[int](1)

	go func() {
		time.Sleep(50 * time.Millisecond)
		ch.Close()
	}()

	require.Equal(t, true, ch.Send(1))
}
