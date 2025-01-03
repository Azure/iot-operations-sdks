// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"strconv"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// SetOption represents a single option for the Set method.
	SetOption interface{ set(*SetOptions) }

	// SetOptions are the resolved options for the Set method.
	SetOptions struct {
		Expiry       time.Duration
		Condition    Condition
		FencingToken hlc.HybridLogicalClock
		Timeout      time.Duration
	}
)

// Set the value of the given key. If the key is successfully set, it returns
// true and the new or updated version; if the key is not set due to the
// specified condition, it returns false and the stored version.
func (c *Client[K, V]) Set(
	ctx context.Context,
	key K,
	val V,
	opt ...SetOption,
) (*Response[bool], error) {
	if len(key) == 0 {
		return nil, ArgumentError{Name: "key"}
	}

	var opts SetOptions
	opts.Apply(opt)

	var rest []string
	if opts.Condition != Always {
		rest = append(rest, string(opts.Condition))
	}
	switch {
	case opts.Expiry < 0:
		return nil, ArgumentError{Name: "Expiry", Value: opts.Expiry}
	case opts.Expiry > 0:
		rest = append(rest, "PX", strconv.Itoa(int(opts.Expiry.Milliseconds())))
	}

	req := resp.OpKV("SET", key, val, rest...)
	return invoke(ctx, c.invoker, parseOK, &opts, req)
}

// Apply resolves the provided list of options.
func (o *SetOptions) Apply(opts []SetOption, rest ...SetOption) {
	for opt := range options.Apply[SetOption](opts, rest...) {
		opt.set(o)
	}
}

func (o *SetOptions) set(opt *SetOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithCondition) set(opt *SetOptions) {
	opt.Condition = Condition(o)
}

// Allow Condition to be used directly as an option for convenience.
func (o Condition) set(opt *SetOptions) {
	opt.Condition = o
}

func (o WithExpiry) set(opt *SetOptions) {
	opt.Expiry = time.Duration(o)
}

func (o WithFencingToken) set(opt *SetOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}

func (o WithTimeout) set(opt *SetOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *SetOptions) invoke() *protocol.InvokeOptions {
	inv := &protocol.InvokeOptions{
		Timeout: o.Timeout,
	}
	if !o.FencingToken.IsZero() {
		inv.Metadata = map[string]string{
			fencingToken: o.FencingToken.String(),
		}
	}
	return inv
}
