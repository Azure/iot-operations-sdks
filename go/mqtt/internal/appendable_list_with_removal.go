// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"iter"
	"sync"
)

// NOTE: this may be moved to the common module once we create one.

type listNode[T any] struct {
	value T
	prev  *listNode[T]
	next  *listNode[T]
}

type AppendableListWithRemoval[T any] struct {
	mu    sync.RWMutex
	first *listNode[T]
	last  *listNode[T]
}

func NewAppendableListWithRemoval[T any]() *AppendableListWithRemoval[T] {
	return &AppendableListWithRemoval[T]{}
}

func (l *AppendableListWithRemoval[T]) AppendEntry(
	value T,
) (removeEntry func()) {
	l.mu.Lock()
	defer l.mu.Unlock()

	node := &listNode[T]{value: value}
	if l.last == nil {
		l.first = node
	} else {
		l.last.next = node
	}
	node.prev = l.last
	l.last = node

	return func() {
		l.mu.Lock()
		defer l.mu.Unlock()
		if node == nil {
			// node was already deleted
			return
		}

		if node.prev == nil {
			l.first = node.next
		} else {
			node.prev.next = node.next
		}

		if node.next == nil {
			l.last = node.prev
		} else {
			node.next.prev = node.prev
		}

		// set this to nil so the node can be garbage collected
		node = nil
	}
}

func (l *AppendableListWithRemoval[T]) All() iter.Seq[T] {
	return func(yield func(T) bool) {
		l.mu.RLock()
		defer l.mu.RUnlock()

		curr := l.first
		for curr != nil && yield(curr.value) {
			curr = curr.next
		}
	}
}
