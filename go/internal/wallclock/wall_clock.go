// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package wallclock

import (
	"context"
	"time"
)

type (
	// WallClock abstracts a subset of functionality from packages context and
	// time.
	WallClock interface {
		WithTimeoutCause(
			parent context.Context,
			timeout time.Duration,
			cause error,
		) (context.Context, context.CancelFunc)
		After(d time.Duration) <-chan time.Time
		NewTimer(d time.Duration) Timer
		Now() time.Time
	}

	// Timer abstracts the functionality of time.Timer.
	Timer interface {
		C() <-chan time.Time
		Reset(d time.Duration) bool
		Stop() bool
	}

	wallClock struct{}

	timer struct {
		*time.Timer
	}
)

// WithTimeoutCause indirects context.WithTimeoutCause.
func (wallClock) WithTimeoutCause(
	parent context.Context,
	timeout time.Duration,
	cause error,
) (context.Context, context.CancelFunc) {
	return context.WithTimeoutCause(parent, timeout, cause)
}

// After indirects time.After.
func (wallClock) After(d time.Duration) <-chan time.Time {
	return time.After(d)
}

// NewTimer indirects time.NewTimer.
func (wallClock) NewTimer(d time.Duration) Timer {
	return timer{Timer: time.NewTimer(d)}
}

// Now indirects time.Now.
func (wallClock) Now() time.Time {
	return time.Now()
}

// C indirects time.Timer.C.
func (t timer) C() <-chan time.Time {
	return t.Timer.C
}

// Instance is a WallClock singleton used for indirect time-based references to
// packages context and time. Test code can set the instance to interpose on
// functions and control apparent time.
var Instance WallClock = wallClock{}
