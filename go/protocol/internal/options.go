package internal

import "iter"

// Apply all non-nil options of a given type.
func Apply[T, O any](opts []O, rest ...O) iter.Seq[T] {
	return func(yield func(T) bool) {
		for _, opt := range opts {
			if op, ok := any(opt).(T); ok && any(op) != nil && !yield(op) {
				return
			}
		}
		for _, opt := range rest {
			if op, ok := any(opt).(T); ok && any(op) != nil && !yield(op) {
				return
			}
		}
	}
}
