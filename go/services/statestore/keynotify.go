// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// KeyNotifyOption represents a single option for the KeyNotify method.
	KeyNotifyOption interface{ keynotify(*KeyNotifyOptions) }

	// KeyNotifyOptions are the resolved options for the KeyNotify method.
	KeyNotifyOptions struct {
		Timeout time.Duration
	}
)

// KeyNotify executes the notification request on the state store in order to
// begin receiving notifications. It should be paired with a KeyNotifyStop call.
func (c *Client[K, V]) KeyNotify(
	ctx context.Context,
	key K,
	opt ...KeyNotifyOption,
) (err error) {
	defer func() { c.logReturn(ctx, err) }()
	if len(key) == 0 {
		return ArgumentError{Name: "key"}
	}

	var opts KeyNotifyOptions
	opts.Apply(opt)

	k := string(key)

	c.keynotifyMu.Lock()
	defer c.keynotifyMu.Unlock()

	c.logK(ctx, "KEYNOTIFY", key)
	req := resp.OpK("KEYNOTIFY", key)
	if _, err := invoke(ctx, c.invoker, parseOK, &opts, req); err != nil {
		return err
	}

	c.keynotify[k]++
	return nil
}

// KeyNotifyStop executes the stop notification request on the state store in
// order to stop receiving notifications. It should only be called once per
// successfull call to KeyNotify (but may be retried in case of failure).
func (c *Client[K, V]) KeyNotifyStop(
	ctx context.Context,
	key K,
	opt ...KeyNotifyOption,
) (err error) {
	defer func() { c.logReturn(ctx, err) }()
	if len(key) == 0 {
		return ArgumentError{Name: "key"}
	}

	var opts KeyNotifyOptions
	opts.Apply(opt)

	k := string(key)

	c.keynotifyMu.Lock()
	defer c.keynotifyMu.Unlock()

	if c.keynotify[k] == 1 {
		c.logK(ctx, "KEYNOTIFY", key, slog.Bool("stop", true))
		req := resp.OpK("KEYNOTIFY", key, "STOP")
		if _, err := invoke(ctx, c.invoker, parseOK, &opts, req); err != nil {
			return err
		}

		delete(c.keynotify, k)
		return nil
	}

	c.keynotify[k]--
	return nil
}

// Apply resolves the provided list of options.
func (o *KeyNotifyOptions) Apply(
	opts []KeyNotifyOption,
	rest ...KeyNotifyOption,
) {
	for opt := range options.Apply[KeyNotifyOption](opts, rest...) {
		opt.keynotify(o)
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
		Timeout: o.Timeout,
	}
}
