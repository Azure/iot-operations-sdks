// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// KeyNotify represents a registered notification request.
	KeyNotify[K, V Bytes] struct {
		Key K

		c      chan Notify[K, V]
		done   chan struct{}
		client *Client[K, V]
		index  int
	}

	// KeyNotifyOption represents a single option for the KeyNotify method.
	KeyNotifyOption interface{ keynotify(*KeyNotifyOptions) }

	// KeyNotifyOptions are the resolved options for the KeyNotify method.
	KeyNotifyOptions struct {
		Timeout time.Duration
	}
)

// KeyNotify requests a notification channel for a key, starting notifications
// if necessary. It returns an object with the channel, which can be used to
// stop this notification request.
func (c *Client[K, V]) KeyNotify(
	ctx context.Context,
	key K,
	opt ...KeyNotifyOption,
) (*KeyNotify[K, V], error) {
	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	k := string(key)
	if len(c.notify[k]) == 0 {
		if err := c.keyNotify(ctx, key, true, opt...); err != nil {
			return nil, err
		}
	}

	// Give the channel a buffer of 1 so we can iterate through them quickly.
	kn := &KeyNotify[K, V]{
		Key:    key,
		c:      make(chan Notify[K, V], 1),
		done:   make(chan struct{}),
		client: c,
		index:  len(c.notify[k]),
	}
	c.notify[k] = append(c.notify[k], kn)

	return kn, nil
}

// C returns the channel used to receive notifications for this key.
func (kn *KeyNotify[K, V]) C() <-chan Notify[K, V] {
	return kn.c
}

// Stop removes this notification and stops notifications for this key if no
// other notifications are registered.
func (kn *KeyNotify[K, V]) Stop(
	ctx context.Context,
	opt ...KeyNotifyOption,
) error {
	// Stop needs to be thread-safe with other keys, but not with itself, and we
	// need to close the done channel outside of the lock to guarantee that the
	// notify loop will unblock (and eventually free the lock).
	if kn.index >= 0 {
		close(kn.done)
	}

	c := kn.client
	k := string(kn.Key)

	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	if kn.index >= 0 {
		// Order doesn't matter, so remove this index quickly by swapping.
		last := len(c.notify[k]) - 1
		c.notify[k][kn.index] = c.notify[k][last]
		c.notify[k][kn.index].index = kn.index
		c.notify[k] = c.notify[k][:last]

		kn.index = -1
		close(kn.c)
	}

	if len(c.notify[k]) == 0 {
		delete(c.notify, k)
		if err := c.keyNotify(ctx, kn.Key, false, opt...); err != nil {
			return err
		}
	}

	return nil
}

// KEYNOTIFY invoke shorthand.
func (c *Client[K, V]) keyNotify(
	ctx context.Context,
	key K,
	run bool,
	opt ...KeyNotifyOption,
) error {
	var data []byte
	if run {
		data = resp.OpK("KEYNOTIFY", key)
	} else {
		data = resp.OpK("KEYNOTIFY", key, "STOP")
	}

	var opts KeyNotifyOptions
	opts.Apply(opt)
	_, err := invoke(ctx, c.invoker, parseOK, &opts, data)
	return err
}

// Apply resolves the provided list of options.
func (o *KeyNotifyOptions) Apply(
	opts []KeyNotifyOption,
	rest ...KeyNotifyOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.keynotify(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.keynotify(o)
		}
	}
}

func (o *KeyNotifyOptions) keynotify(opt *KeyNotifyOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithTimeout) keynotify(opt *KeyNotifyOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *KeyNotifyOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
	}
}
