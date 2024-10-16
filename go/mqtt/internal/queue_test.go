// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal_test

import (
	"sync"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/stretchr/testify/require"
)

func TestQueue(t *testing.T) {
	q := internal.NewQueue[int](100)

	for i := 100; i > 0; i-- {
		q.Enqueue(i)
	}

	for i := 100; i > 0; i-- {
		value := q.Dequeue()
		require.Equal(t, i, *value)
	}

	// Should be empty now.
	require.True(t, q.IsEmpty())
}

func TestQueueOrder(t *testing.T) {
	q := internal.NewQueue[int](100)

	for i := 0; i < 50; i++ {
		q.Enqueue(i)
	}

	for i := 0; i < 10; i++ {
		value := q.Dequeue()
		require.Equal(t, i, *value)
	}

	for i := 50; i < 100; i++ {
		q.Enqueue(i)
	}

	for i := 10; i < 100; i++ {
		value := q.Dequeue()
		require.Equal(t, i, *value)
	}
}

func TestQueueMaxSize(t *testing.T) {
	q := internal.NewQueue[int](10)

	for i := 0; i < 11; i++ {
		q.Enqueue(i)
	}

	require.True(t, q.IsFull())

	for i := 0; i < 10; i++ {
		value := q.Dequeue()
		require.Equal(t, i, *value)
	}

	for i := 10; i < 100; i++ {
		value := q.Dequeue()
		require.Nil(t, value)
	}
}

func TestQueueAsync(t *testing.T) {
	q := internal.NewQueue[int](100)
	var wg sync.WaitGroup

	// Start multiple goroutines to enqueue elements.
	for i := 0; i < 100; i++ {
		wg.Add(1)
		go func(val int) {
			defer wg.Done()
			q.Enqueue(val)
		}(i)
	}
	wg.Wait()

	// Dequeue elements and check that all expected values are present
	seen := make(map[int]bool)
	for i := 0; i < 100; i++ {
		value := q.Dequeue()
		require.NotNil(t, value)
		seen[*value] = true
	}

	// Verify.
	for i := 0; i < 100; i++ {
		require.True(t, seen[i], i)
	}
}
