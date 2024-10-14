// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"sync"
)

// BufferChan is a concurrency-safe generic buffered channel.
type BufferChan[T comparable] struct {
	C      chan T
	Mu     sync.RWMutex
	Closed bool
}

func NewBufferChan[T comparable](size int) *BufferChan[T] {
	return &BufferChan[T]{
		C: make(chan T, size),
	}
}

func (ch *BufferChan[T]) Send(value T) bool {
	ch.Mu.RLock()
	defer ch.Mu.RUnlock()

	if ch.Closed {
		return false
	}

	select {
	case ch.C <- value:
		return true
	default:
		return false
	}
}

func (ch *BufferChan[T]) Close() {
	ch.Mu.Lock()
	defer ch.Mu.Unlock()

	if !ch.Closed && ch.C != nil {
		close(ch.C)
		ch.Closed = true
		ch.C = nil
	}
}
