package container

import (
	"iter"
	"sync"
)

// Thread-safe generic map.
type SyncMap[K comparable, V any] struct {
	m map[K]V
	l sync.RWMutex
}

func NewSyncMap[K comparable, V any]() SyncMap[K, V] {
	return SyncMap[K, V]{m: map[K]V{}}
}

func (s *SyncMap[K, V]) Get(key K) (V, bool) {
	s.l.RLock()
	defer s.l.RUnlock()
	val, ok := s.m[key]
	return val, ok
}

func (s *SyncMap[K, V]) Set(key K, val V) {
	s.l.Lock()
	defer s.l.Unlock()
	s.m[key] = val
}

func (s *SyncMap[K, V]) Del(key K) {
	s.l.Lock()
	defer s.l.Unlock()
	delete(s.m, key)
}

func (s *SyncMap[K, V]) All() iter.Seq2[K, V] {
	return func(yield func(K, V) bool) {
		s.l.RLock()
		defer s.l.RUnlock()
		for k, v := range s.m {
			if !yield(k, v) {
				return
			}
		}
	}
}
