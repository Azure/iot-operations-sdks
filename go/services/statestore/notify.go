// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"encoding/hex"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

// Notify represents a notification event.
type Notify[K, V Bytes] struct {
	Key       K
	Operation string
	Value     V
	Version   hlc.HybridLogicalClock

	// Ack provides a function to manually ack if enabled; it will be nil
	// otherwise.
	Ack func() error
}

// Notify requests a notification channel for a key. It returns the channel and
// a function to remove and close that channel. Note that KeyNotify must be
// called to actually perform the notification request (though notifications may
// be received on this channel if KeyNotify had already been called previously).
// Also please note that the state store does not queue messages when the client
// is disconnected, therefore notifications received on this channel are not
// guaranteed, may be duplicated, and may come out-of-order during reconnection.
func (c *Client[K, V]) Notify(key K) (<-chan Notify[K, V], func()) {
	k := string(key)

	// Give the channel a buffer of 1 so we can iterate through them quickly.
	ch := make(chan Notify[K, V], 1)
	done := make(chan struct{})

	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	kn, ok := c.notify[k]
	if !ok {
		kn = map[chan Notify[K, V]]chan struct{}{}
		c.notify[k] = kn
	}
	kn[ch] = done

	return ch, sync.OnceFunc(func() {
		close(done)

		c.notifyMu.Lock()
		defer c.notifyMu.Unlock()

		close(ch)

		delete(kn, ch)
		if len(kn) == 0 {
			delete(c.notify, k)
		}
	})
}

// Receive a NOTIFY message.
func (c *Client[K, V]) notifyReceive(
	ctx context.Context,
	msg *protocol.TelemetryMessage[[]byte],
) error {
	hexKey, ok := msg.TopicTokens["keyName"]
	if !ok {
		return resp.PayloadError("missing key name")
	}

	bytKey, err := hex.DecodeString(hexKey)
	if err != nil {
		return resp.PayloadError("invalid key name %q", hexKey)
	}

	data, err := resp.BlobArray[[]byte](msg.Payload)
	if err != nil {
		return err
	}

	opOnly := len(data) == 2
	hasValue := len(data) == 4

	if (!opOnly && !hasValue) ||
		(string(data[0]) != "NOTIFY") ||
		(hasValue && string(data[2]) != "VALUE") {
		return resp.PayloadError("invalid payload %q", string(msg.Payload))
	}

	key := K(bytKey)
	op := string(data[1])
	var val V
	if hasValue {
		val = V(data[3])
	}

	c.notifySend(ctx, &Notify[K, V]{key, op, val, msg.Timestamp, msg.Ack})

	return nil
}

func (c *Client[K, V]) notifySend(ctx context.Context, notify *Notify[K, V]) {
	// TODO: Lock less globally if possible, but keep it simple for now.
	c.notifyMu.RLock()
	defer c.notifyMu.RUnlock()

	for ch, done := range c.notify[string(notify.Key)] {
		select {
		case ch <- *notify:
		case <-done:
		case <-ctx.Done():
		}
	}
}
