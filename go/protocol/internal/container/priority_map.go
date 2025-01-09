// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package container

import "container/heap"

type (
	// PriorityMap provides a map with a built-in priority queue for trimming.
	PriorityMap[K comparable, V any, P Priority] struct {
		q priorityQueue[K, V, P]
		m map[K]*entry[K, V, P]
	}

	// Priority defines the number types available to use as a priority value.
	Priority interface{ ~int64 | ~float64 }

	// https://pkg.go.dev/container/heap#example-package-PriorityQueue
	priorityQueue[K comparable, V any, P Priority] []*entry[K, V, P]

	entry[K comparable, V any, P Priority] struct {
		key K
		val V
		pri P
		idx int
	}
)

func (pq priorityQueue[K, V, P]) Len() int {
	return len(pq)
}

func (pq priorityQueue[K, V, P]) Less(i, j int) bool {
	return pq[i].pri < pq[j].pri
}

func (pq priorityQueue[K, V, P]) Swap(i, j int) {
	pq[i], pq[j] = pq[j], pq[i]
	pq[i].idx = i
	pq[j].idx = j
}

func (pq *priorityQueue[K, V, P]) Push(v any) {
	//nolint:forcetypeassert // The type is guaranteed by the implementation.
	e := v.(*entry[K, V, P])
	e.idx = len(*pq)
	*pq = append(*pq, e)
}

func (pq *priorityQueue[K, V, P]) Pop() any {
	o := *pq
	n := len(o)
	e := o[n-1]
	o[n-1] = nil
	*pq = o[0 : n-1]
	return e
}

// Len returns the number of elements in the queue.
func (p *PriorityMap[K, V, P]) Len() int {
	return len(p.q)
}

// Get an element in the map by its key.
func (p *PriorityMap[K, V, P]) Get(key K) (V, bool) {
	if e, ok := p.m[key]; ok {
		return e.val, true
	}
	var zv V
	return zv, false
}

// Set an element in the map to the given value and priority.
func (p *PriorityMap[K, V, P]) Set(key K, val V, pri P) {
	if e, ok := p.m[key]; ok {
		e.val = val
		e.pri = pri
		heap.Fix(&p.q, e.idx)
	} else {
		e := &entry[K, V, P]{key, val, pri, 0}
		p.m[key] = e
		heap.Push(&p.q, e)
	}
}

// Next returns the next value in the map with the lowest priority.
func (p *PriorityMap[K, V, P]) Next() (K, V, bool) {
	if len(p.q) == 0 {
		var zk K
		var zv V
		return zk, zv, false
	}
	e := p.q[0]
	return e.key, e.val, true
}

// Delete an element from the map.
func (p *PriorityMap[K, V, P]) Delete(key K) {
	if e, ok := p.m[key]; ok {
		heap.Remove(&p.q, e.idx)
		delete(p.m, key)
	}
}

// NewPriorityMap creates a new empty priority queue.
func NewPriorityMap[K comparable, V any, P Priority]() PriorityMap[K, V, P] {
	return PriorityMap[K, V, P]{m: map[K]*entry[K, V, P]{}}
}
