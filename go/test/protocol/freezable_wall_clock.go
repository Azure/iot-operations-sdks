package protocol

import (
	"context"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
)

type (
	freezableWallClock struct {
		mu             sync.Mutex
		nextTicket     int
		activeTickets  map[int]struct{}
		isFrozen       bool
		timeOffset     time.Duration
		frozenTime     time.Time
		pausableTimers []*pausableTimer
	}
)

var once sync.Once

func enableFreezing() {
	once.Do(func() {
		wallclock.Instance = newFreezableWallClock()
	})
}

func newFreezableWallClock() *freezableWallClock {
	return &freezableWallClock{
		nextTicket:     0,
		activeTickets:  make(map[int]struct{}),
		isFrozen:       false,
		timeOffset:     0,
		pausableTimers: make([]*pausableTimer, 0),
	}
}

func (w *freezableWallClock) WithTimeoutCause(
	parent context.Context,
	timeout time.Duration,
	cause error,
) (context.Context, context.CancelFunc) {
	ctx, cancelCause := context.WithCancelCause(parent)

	go func(t wallclock.Timer) {
		select {
		case <-ctx.Done():
			t.Stop()
		case <-t.C():
			cancelCause(cause)
		}
	}(w.NewTimer(timeout))

	return ctx, func() { cancelCause(nil) }
}

func (w *freezableWallClock) After(d time.Duration) <-chan time.Time {
	return w.NewTimer(d).C()
}

func (w *freezableWallClock) NewTimer(d time.Duration) wallclock.Timer {
	w.mu.Lock()
	defer w.mu.Unlock()

	pausableTimer := newPausableTimer(d, w.isFrozen)
	w.pausableTimers = append(w.pausableTimers, pausableTimer)
	return pausableTimer
}

func (w *freezableWallClock) Now() time.Time {
	w.mu.Lock()
	defer w.mu.Unlock()

	if w.isFrozen {
		return w.frozenTime
	}
	return time.Now().Add(w.timeOffset)
}

func (w *freezableWallClock) freezeTime() int {
	w.mu.Lock()
	defer w.mu.Unlock()

	if !w.isFrozen {
		w.frozenTime = time.Now().Add(w.timeOffset)
		w.isFrozen = true

		newPausableTimers := make([]*pausableTimer, 0, len(w.pausableTimers))

		for _, pausableTimer := range w.pausableTimers {
			if pausableTimer.pause() {
				newPausableTimers = append(newPausableTimers, pausableTimer)
			}
		}

		w.pausableTimers = newPausableTimers
	}

	newTicket := w.nextTicket
	w.nextTicket++

	w.activeTickets[newTicket] = struct{}{}

	return newTicket
}

func (w *freezableWallClock) unfreezeTime(ticket int) {
	w.mu.Lock()
	defer w.mu.Unlock()

	if !w.isFrozen {
		panic("freezableWallClock.unfreezeTime(): clock already unfrozen")
	}

	if _, ok := w.activeTickets[ticket]; !ok {
		panic("freezableWallClock.unfreezeTime(): ticket not outstanding")
	}

	delete(w.activeTickets, ticket)

	if len(w.activeTickets) == 0 {
		w.timeOffset = time.Until(w.frozenTime)
		w.isFrozen = false

		for _, pausableTimer := range w.pausableTimers {
			pausableTimer.resume()
		}
	}
}
