package container

import "sync"

// Thread-safe generic map.
type SyncMap[K comparable, V any] struct {
	m map[K]V
	l sync.RWMutex
}

func NewSyncMap[K comparable, V any]() SyncMap[K, V] {
	return SyncMap[K, V]{m: map[K]V{}}
}

func (s *SyncMap[K, V]) Load(key K) (V, bool) {
	s.l.RLock()
	defer s.l.RUnlock()
	val, ok := s.m[key]
	return val, ok
}

func (s *SyncMap[K, V]) Store(key K, val V) {
	s.l.Lock()
	defer s.l.Unlock()
	s.m[key] = val
}

func (s *SyncMap[K, V]) Delete(key K) {
	s.l.Lock()
	defer s.l.Unlock()
	delete(s.m, key)
}

func (s *SyncMap[K, V]) Range(f func(k K, v V) bool) {
	s.l.RLock()
	defer s.l.RUnlock()
	for k, v := range s.m {
		if !f(k, v) {
			return
		}
	}
}
