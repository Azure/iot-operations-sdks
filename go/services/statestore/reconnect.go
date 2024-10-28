// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

// Resubscribe to key notifications when the client reconnects.
func (c *Client[K, V]) reconnect(ctx context.Context) {
	c.keynotifyMu.RLock()
	defer c.keynotifyMu.RUnlock()

	var opts KeyNotifyOptions
	for k := range c.keynotify {
		key := K(k)

		// We call KEYNOTIFY raw to not touch the refcount (or more importantly
		// the lock we're currently under).
		//nolint:errcheck // TODO: Is there anything useful to do if this fails?
		// Even bailing out of the loop is unnecessary since we can still return
		// the latest value as a best effort.
		invoke(ctx, c.invoker, parseOK, &opts, resp.OpK("KEYNOTIFY", key))

		// Get the latest value and artificially generate a notification.
		res, err := c.Get(ctx, key)
		if err != nil {
			continue
		}

		op := "SET"
		if res.Version.IsZero() {
			op = "DELETE"
		}

		// Ack isn't actually meaningful here, but include the callback if
		// appropriate to not break user code that is expecting it.
		var ack func() error
		if c.manualAck {
			ack = func() error { return nil }
		}

		c.notifySend(ctx, &Notify[K, V]{key, op, res.Value, res.Version, ack})
	}
}
