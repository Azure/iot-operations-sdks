// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"sync"
)

// Queue is a concurrency-safe generic circular queue.
type Queue[T comparable] struct {
	mu      sync.RWMutex
	items   []T
	maxSize int // Limits the queue size to prevent indefinite growth
	size    int
	enter   int // Points to the next position for entering
	leave   int // Points to the next item that is leaving
}

// NewQueue creates a new Queue.
func NewQueue[T comparable](maxSize int) *Queue[T] {
	return &Queue[T]{
		items:   make([]T, 0),
		maxSize: maxSize,
	}
}

// Size returns the number of items in the queue.
func (q *Queue[T]) Size() int {
	q.mu.RLock()
	defer q.mu.RUnlock()

	return q.size
}

// Enqueue adds an item to the end of the queue.
func (q *Queue[T]) Enqueue(value T) {
	q.mu.Lock()
	defer q.mu.Unlock()

	// Stop enqueuing if the queue reaches its maximum size.
	if q.size == q.maxSize {
		return
	}

	// Resize at the very beginning or when the queue is full
	if len(q.items) == 0 || len(q.items) == q.size {
		q.resize()
	}

	q.items[q.enter] = value
	q.enter = q.move(q.enter)
	q.size++
}

// Dequeue removes and returns pointer of the item from the front of the queue.
func (q *Queue[T]) Dequeue() *T {
	q.mu.Lock()
	defer q.mu.Unlock()

	if q.size == 0 {
		return nil
	}

	item := q.items[q.leave]
	q.leave = q.move(q.leave)
	q.size--
	return &item
}

// IsEmpty returns whether the queue is empty.
func (q *Queue[T]) IsEmpty() bool {
	q.mu.RLock()
	defer q.mu.RUnlock()

	return q.size == 0
}

// IsFull returns whether the queue is full.
func (q *Queue[T]) IsFull() bool {
	q.mu.RLock()
	defer q.mu.RUnlock()

	// If it's full, len(q.items) is no less than q.maxSize
	// otherwise we resize/expand the slice,
	// so we can't compare q.size with it.
	return q.size > 0 && q.size == q.maxSize
}

// Clear removes all elements from the queue.
func (q *Queue[T]) Clear() {
	q.mu.Lock()
	defer q.mu.Unlock()

	q.items = make([]T, 0)
	q.enter = 0
	q.leave = 0
	q.size = 0
}

// resize increases the size of the queue.
// TODO: Add support for decreasing the size.
func (q *Queue[T]) resize() {
	oldSize := len(q.items)
	newSize := len(q.items)*2 + 1

	// [4,5,1,2,3] => [4,5,(1),(2),(3),_,_,_,1,2,3]
	// q.enter = 2, q.leave = 2
	// oldSize = 5, newSize = 11
	// oldLeave = 2, newLeave = 11 - (5 - 2) = 8
	// q.enter = 2, q.leave = 8
	oldLeave := q.leave
	newLeave := newSize - (oldSize - oldLeave)
	// Initial case.
	if len(q.items) == 0 {
		newLeave = 0
	}

	// Expand slice.
	q.items = append(q.items, make([]T, newSize-oldSize)...)

	// Move items.
	copy(q.items[newLeave:], q.items[oldLeave:oldSize])
	q.leave = newLeave
}

// move increments the index circularly.
func (q *Queue[T]) move(index int) int {
	return (index + 1) % len(q.items)
}
