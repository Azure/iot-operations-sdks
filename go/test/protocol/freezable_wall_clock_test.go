package protocol

import (
	"context"
	stderr "errors"
	"testing"
	"time"

	"github.com/stretchr/testify/require"
)

const assumedClockResolution = 16 * time.Millisecond

var (
	errCustom1 = stderr.New("custom error 1")
	errCustom2 = stderr.New("custom error 2")
)

func TestNeverFrozenClockTracksRealTime(t *testing.T) {
	t.Parallel()

	freezableWallClock := newFreezableWallClock()

	lowerBound := time.Now()
	testTime := freezableWallClock.Now()
	upperBound := time.Now()
	require.True(t, !testTime.Before(lowerBound))
	require.True(t, !testTime.After(upperBound))

	time.Sleep(2 * time.Second)

	lowerBound = time.Now()
	testTime = freezableWallClock.Now()
	upperBound = time.Now()
	require.True(t, !testTime.Before(lowerBound))
	require.True(t, !testTime.After(upperBound))
}

func TestFrozenClockDoesNotAdvance(t *testing.T) {
	t.Parallel()

	delayDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	freezableWallClock.freezeTime()
	realTimeEarly := time.Now()
	testTimeEarly := freezableWallClock.Now()

	time.Sleep(delayDuration)
	realTimeLate := time.Now()
	testTimeLate := freezableWallClock.Now()

	require.True(
		t,
		!realTimeEarly.Add(delayDuration).
			After(realTimeLate.Add(assumedClockResolution)),
	)
	require.Equal(t, testTimeEarly, testTimeLate)
}

func TestFrozenThenUnfrozenClockMaintainsFixedOffset(t *testing.T) {
	t.Parallel()

	delayDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()
	time.Sleep(delayDuration)
	freezableWallClock.unfreezeTime(ticket)

	lowerBound := time.Now()
	testTime := freezableWallClock.Now()
	upperBound := time.Now()

	minOffset := lowerBound.Sub(testTime)
	maxOffset := upperBound.Sub(testTime)

	time.Sleep(2 * time.Second)

	lowerBound = time.Now().Add(-maxOffset)
	testTime = freezableWallClock.Now()
	upperBound = time.Now().Add(-minOffset)

	require.True(t, !testTime.Before(lowerBound))
	require.True(t, !testTime.After(upperBound))
}

func TestUnfreezeUnfrozenClockPanics(t *testing.T) {
	t.Parallel()

	freezableWallClock := newFreezableWallClock()

	defer func() {
		if r := recover(); r == nil {
			require.Failf(
				t,
				"no panic",
				"unfreezeTime() on unfrozen clock failed to panic",
			)
		}
	}()

	freezableWallClock.unfreezeTime(0)
}

func TestUnfreezeNonIssuedTicketPanics(t *testing.T) {
	t.Parallel()

	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	defer func() {
		if r := recover(); r == nil {
			require.Failf(
				t,
				"no panic",
				"unfreezeTime() with non-issued ticket failed to panic",
			)
		}
	}()

	freezableWallClock.unfreezeTime(ticket + 1)
}

func TestDoubleUnfreezePanics(t *testing.T) {
	t.Parallel()

	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	freezableWallClock.unfreezeTime(ticket)

	defer func() {
		if r := recover(); r == nil {
			require.Failf(
				t,
				"no panic",
				"double unfreezeTime() failed to panic",
			)
		}
	}()

	freezableWallClock.unfreezeTime(ticket)
}

func TestMatchedSingularFreezeUnfreezeRestoresAdvancement(t *testing.T) {
	t.Parallel()

	delayDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	realTimeEarly := time.Now()
	testTimeEarly := freezableWallClock.Now()

	freezableWallClock.unfreezeTime(ticket)

	time.Sleep(delayDuration)
	realTimeLate := time.Now()
	testTimeLate := freezableWallClock.Now()

	require.True(
		t,
		!realTimeEarly.Add(delayDuration).
			After(realTimeLate.Add(assumedClockResolution)),
	)
	require.True(
		t,
		!testTimeEarly.Add(delayDuration).
			After(testTimeLate.Add(assumedClockResolution)),
	)
}

func TestMatchedPluralFreezeUnfreezeRestoresAdvancement(t *testing.T) {
	t.Parallel()

	delayDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket0 := freezableWallClock.freezeTime()
	ticket1 := freezableWallClock.freezeTime()

	realTimeEarly := time.Now()
	testTimeEarly := freezableWallClock.Now()

	freezableWallClock.unfreezeTime(ticket0)
	freezableWallClock.unfreezeTime(ticket1)

	time.Sleep(delayDuration)
	realTimeLate := time.Now()
	testTimeLate := freezableWallClock.Now()

	require.True(
		t,
		!realTimeEarly.Add(delayDuration).
			After(realTimeLate.Add(assumedClockResolution)),
	)
	require.True(
		t,
		!testTimeEarly.Add(delayDuration).
			After(testTimeLate.Add(assumedClockResolution)),
	)
}

func TestUnmatchedPluralFreezeSingularUnfreezeMaintainsFreeze(t *testing.T) {
	t.Parallel()

	delayDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket0 := freezableWallClock.freezeTime()
	freezableWallClock.freezeTime()

	realTimeEarly := time.Now()
	testTimeEarly := freezableWallClock.Now()

	freezableWallClock.unfreezeTime(ticket0)

	time.Sleep(delayDuration)
	realTimeLate := time.Now()
	testTimeLate := freezableWallClock.Now()

	require.True(
		t,
		!realTimeEarly.Add(delayDuration).
			After(realTimeLate.Add(assumedClockResolution)),
	)
	require.Equal(t, testTimeEarly, testTimeLate)
}

func TestNewTimerWhenClockUnfrozen(t *testing.T) {
	t.Parallel()

	waitDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	timer := freezableWallClock.NewTimer(waitDuration)
	<-timer.C()

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestAfterWhenClockUnfrozen(t *testing.T) {
	t.Parallel()

	waitDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	timeChan := freezableWallClock.After(waitDuration)
	<-timeChan

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestNewTimerDuringFreeze(t *testing.T) {
	t.Parallel()

	waitDuration := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	startTime := freezableWallClock.Now()

	timer := freezableWallClock.NewTimer(waitDuration)

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-timer.C()

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestAfterDuringFreeze(t *testing.T) {
	t.Parallel()

	waitDuration := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	startTime := freezableWallClock.Now()

	timeChan := freezableWallClock.After(waitDuration)

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-timeChan

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestNewTimerBeforeFreeze(t *testing.T) {
	t.Parallel()

	waitDuration := 2 * time.Second
	freezeStart := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	timer := freezableWallClock.NewTimer(waitDuration)

	time.Sleep(freezeStart)
	ticket := freezableWallClock.freezeTime()

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-timer.C()

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestAfterBeforeFreeze(t *testing.T) {
	t.Parallel()

	waitDuration := 2 * time.Second
	freezeStart := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	timeChan := freezableWallClock.After(waitDuration)

	time.Sleep(freezeStart)
	ticket := freezableWallClock.freezeTime()

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-timeChan

	require.True(
		t,
		!startTime.Add(waitDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
}

func TestWithTimeoutCauseWhenClockUnfrozenTimeout(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	timeoutDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	ctx, _ := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)
	<-ctx.Done()

	require.True(
		t,
		!startTime.Add(timeoutDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
	require.True(t, stderr.Is(context.Cause(ctx), errCustom1))
}

func TestWithTimeoutCauseWhenClockUnfrozenCancel(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	cancelDuration := 1 * time.Second
	timeoutDuration := 3 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	ctx, cancel := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(cancelDuration)
	cancel()

	<-ctx.Done()

	require.True(
		t,
		startTime.Add(timeoutDuration).After(freezableWallClock.Now()),
	)
	require.True(t, stderr.Is(context.Cause(ctx), context.Canceled))
}

func TestWithTimeoutCauseWhenClockUnfrozenParentCancel(t *testing.T) {
	t.Parallel()

	parentCtx, cancel := context.WithCancelCause(context.Background())
	cancelDuration := 1 * time.Second
	timeoutDuration := 3 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	ctx, _ := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(cancelDuration)
	cancel(errCustom2)

	<-ctx.Done()

	require.True(
		t,
		startTime.Add(timeoutDuration).After(freezableWallClock.Now()),
	)
	require.True(t, stderr.Is(context.Cause(ctx), errCustom2))
}

func TestWithTimeoutCauseDuringFreezeTimeout(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	timeoutDuration := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	ticket := freezableWallClock.freezeTime()

	startTime := freezableWallClock.Now()

	ctx, _ := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-ctx.Done()

	require.True(
		t,
		!startTime.Add(timeoutDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
	require.True(t, stderr.Is(context.Cause(ctx), errCustom1))
}

func TestWithTimeoutCauseBeforeFreezeTimeout(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	timeoutDuration := 2 * time.Second
	freezeStart := 1 * time.Second
	freezeDuration := 2 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	ctx, _ := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(freezeStart)
	ticket := freezableWallClock.freezeTime()

	time.Sleep(freezeDuration)
	freezableWallClock.unfreezeTime(ticket)

	<-ctx.Done()

	require.True(
		t,
		!startTime.Add(timeoutDuration).
			After(freezableWallClock.Now().Add(assumedClockResolution)),
	)
	require.True(t, stderr.Is(context.Cause(ctx), errCustom1))
}

func TestWithTimeoutCauseDuringFreezeCancel(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	cancelDuration := 1 * time.Second
	timeoutDuration := 3 * time.Second
	freezableWallClock := newFreezableWallClock()

	freezableWallClock.freezeTime()

	startTime := freezableWallClock.Now()

	ctx, cancel := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(cancelDuration)
	cancel()

	<-ctx.Done()

	require.True(
		t,
		startTime.Add(timeoutDuration).After(freezableWallClock.Now()),
	)
	require.True(t, stderr.Is(context.Cause(ctx), context.Canceled))
}

func TestWithTimeoutCauseBeforeFreezeCancel(t *testing.T) {
	t.Parallel()

	parentCtx := context.Background()
	cancelDuration := 1 * time.Second
	timeoutDuration := 3 * time.Second
	freezeStart := 1 * time.Second
	freezableWallClock := newFreezableWallClock()

	startTime := freezableWallClock.Now()

	ctx, cancel := freezableWallClock.WithTimeoutCause(
		parentCtx,
		timeoutDuration,
		errCustom1,
	)

	time.Sleep(freezeStart)
	freezableWallClock.freezeTime()

	time.Sleep(cancelDuration)
	cancel()

	<-ctx.Done()

	require.True(
		t,
		startTime.Add(timeoutDuration).After(freezableWallClock.Now()),
	)
	require.True(t, stderr.Is(context.Cause(ctx), context.Canceled))
}
