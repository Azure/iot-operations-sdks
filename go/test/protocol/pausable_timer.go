package protocol

import (
	"time"
)

type (
	pausableTimer struct {
		paused         bool
		timer          *time.Timer
		remainingDelay time.Duration // stale unless paused
		startTime      time.Time     // invalid unless !paused
	}
)

func newPausableTimer(d time.Duration, startPaused bool) *pausableTimer {
	var newTimer *time.Timer
	if startPaused {
		newTimer = time.NewTimer(time.Hour)
		newTimer.Stop()
	} else {
		newTimer = time.NewTimer(d)
	}

	return &pausableTimer{
		paused:         startPaused,
		timer:          newTimer,
		remainingDelay: d,
		startTime:      time.Now().UTC(),
	}
}

func (t pausableTimer) C() <-chan time.Time {
	return t.timer.C
}

func (t *pausableTimer) Reset(d time.Duration) bool {
	t.remainingDelay = d
	t.startTime = time.Now().UTC()
	if t.paused {
		return true
	}
	return t.timer.Reset(d)
}

func (t *pausableTimer) Stop() bool {
	t.remainingDelay = 0
	return t.timer.Stop()
}

func (t *pausableTimer) pause() bool {
	if t.paused {
		panic("pausableTimer.pause(): timer already paused")
	}

	t.paused = true
	if t.timer.Stop() {
		t.remainingDelay -= time.Since(t.startTime)
		return true
	}
	t.remainingDelay = 0
	return false
}

func (t *pausableTimer) resume() {
	if !t.paused {
		panic("pausableTimer.resume(): timer not paused")
	}

	t.paused = false
	t.startTime = time.Now().UTC()
	t.timer.Reset(t.remainingDelay)
}
