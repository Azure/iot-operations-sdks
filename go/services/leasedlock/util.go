// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package leasedlock

import "context"

// A simple mutex-like construct built on a channel to allow context
// cancellation. Must be initialized to a channel of size 1.
type much chan struct{}

func (mc much) Lock(ctx context.Context) error {
	select {
	case mc <- struct{}{}:
		return nil
	case <-ctx.Done():
		return context.Cause(ctx)
	}
}

func (mc much) Unlock() {
	<-mc
}
