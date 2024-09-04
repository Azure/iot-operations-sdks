package container

import "container/heap"

type (
	// PriorityQueue provides a generic priority queue implementation with a
	// built-in index for removals.
	// https://pkg.go.dev/container/heap#example-package-PriorityQueue
	PriorityQueue[T comparable, P Priority] struct {
		queue priorityQueue[T, P]
		index map[T]*item[T, P]
	}

	// Priority defines the number types available to use as a priority value.
	Priority interface{ int64 | float64 }

	priorityQueue[T any, P Priority] []*item[T, P]

	item[T any, P Priority] struct {
		val T
		pri P
		idx int
	}
)

func (pq priorityQueue[T, P]) Len() int {
	return len(pq)
}

func (pq priorityQueue[T, P]) Less(i, j int) bool {
	return pq[i].pri < pq[j].pri
}

func (pq priorityQueue[T, P]) Swap(i, j int) {
	pq[i], pq[j] = pq[j], pq[i]
	pq[i].idx = i
	pq[j].idx = j
}

func (pq *priorityQueue[T, P]) Push(e any) {
	//nolint:forcetypeassert // The type is guaranteed by the implementation.
	i := e.(*item[T, P])
	i.idx = len(*pq)
	*pq = append(*pq, i)
}

func (pq *priorityQueue[T, P]) Pop() any {
	o := *pq
	n := len(o)
	e := o[n-1]
	o[n-1] = nil
	*pq = o[0 : n-1]
	return e
}

// Len returns the number of elements in the queue.
func (pq *PriorityQueue[T, P]) Len() int {
	return pq.queue.Len()
}

// Push pushes an element into the queue.
func (pq *PriorityQueue[T, P]) Push(val T, pri P) {
	i := &item[T, P]{val: val, pri: pri}
	pq.index[val] = i
	heap.Push(&pq.queue, i)
}

// Pop pops an element from the queue.
func (pq *PriorityQueue[T, P]) Pop() (T, bool) {
	if len(pq.queue) == 0 {
		var zero T
		return zero, false
	}
	//nolint:forcetypeassert // The type is guaranteed by the implementation.
	e := heap.Pop(&pq.queue).(*item[T, P])
	delete(pq.index, e.val)
	return e.val, true
}

// Peek returns the top element from the queue without removing it.
func (pq *PriorityQueue[T, P]) Peek() (T, bool) {
	if len(pq.queue) == 0 {
		var zero T
		return zero, false
	}
	e := pq.queue[0]
	return e.val, true
}

// Remove removes an arbitrary element from the queue.
func (pq *PriorityQueue[T, P]) Remove(val T) bool {
	if i, ok := pq.index[val]; ok {
		heap.Remove(&pq.queue, i.idx)
		delete(pq.index, i.val)
		return true
	}
	return false
}

// NewPriorityQueue creates a new empty priority queue.
func NewPriorityQueue[T comparable, P Priority]() PriorityQueue[T, P] {
	return PriorityQueue[T, P]{index: map[T]*item[T, P]{}}
}
