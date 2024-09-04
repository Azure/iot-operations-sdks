package internal

import "sync"

// Set is a concurrency-safe generic set.
type Set[T comparable] struct {
	mu    sync.RWMutex
	items map[T]struct{}
}

// NewSet creates a new set.
func NewSet[T comparable]() *Set[T] {
	return &Set[T]{
		items: make(map[T]struct{}),
	}
}

// Add adds an element to the set.
func (s *Set[T]) Add(value T) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.items[value] = struct{}{}
}

// Remove removes an element from the set.
func (s *Set[T]) Remove(value T) {
	s.mu.Lock()
	defer s.mu.Unlock()

	delete(s.items, value)
}

// Contains checks if an element is in the set.
func (s *Set[T]) Contains(value T) bool {
	s.mu.RLock()
	defer s.mu.RUnlock()

	_, exists := s.items[value]
	return exists
}

// Size returns the number of elements in the set.
func (s *Set[T]) Size() int {
	s.mu.RLock()
	defer s.mu.RUnlock()

	return len(s.items)
}

// Clear removes all elements from the set.
func (s *Set[T]) Clear() {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.items = make(map[T]struct{})
}
