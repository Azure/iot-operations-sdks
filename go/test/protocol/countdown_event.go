// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"errors"
	"sync/atomic"
)

type CountdownEvent struct {
	remainingCount int64
	signalChan     chan struct{}
}

func NewCountdownEvent(initialCount int) *CountdownEvent {
	return &CountdownEvent{
		remainingCount: int64(initialCount),
		signalChan:     make(chan struct{}, initialCount),
	}
}

func (c *CountdownEvent) Wait(ctx context.Context) error {
	if c.remainingCount <= 0 {
		return nil
	}

	select {
	case <-c.signalChan:
		c.signalChan <- struct{}{}
		return nil
	case <-ctx.Done():
		return errors.New("CountdownEvent canceled")
	}
}

func (c *CountdownEvent) Signal() {
	if atomic.AddInt64(&c.remainingCount, -1) <= 0 {
		c.signalChan <- struct{}{}
	}
}
