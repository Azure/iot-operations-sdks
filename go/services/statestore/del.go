// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// DelOption represents a single option for the Del method.
	DelOption interface{ del(*DelOptions) }

	// DelOptions are the resolved options for the Del method.
	DelOptions struct {
		FencingToken hlc.HybridLogicalClock
		Timeout      time.Duration
	}
)

// Del deletes the given key. It returns the number of keys deleted
// (typically 0 or 1).
func (c *Client[K, V]) Del(
	ctx context.Context,
	key K,
	opt ...DelOption,
) (res *Response[int], err error) {
	defer func() { c.logReturn(ctx, err) }()
	if len(key) == 0 {
		return nil, ArgumentError{Name: "key"}
	}

	var opts DelOptions
	opts.Apply(opt)

	c.logOp(ctx, "DEL", key)
	req := resp.OpK("DEL", key)
	return invoke(ctx, c.invoker, resp.Number, &opts, req)
}

// Apply resolves the provided list of options.
func (o *DelOptions) Apply(opts []DelOption, rest ...DelOption) {
	for opt := range options.Apply[DelOption](opts, rest...) {
		opt.del(o)
	}
}

func (o *DelOptions) del(opt *DelOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithFencingToken) del(opt *DelOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}

func (o WithTimeout) del(opt *DelOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *DelOptions) invoke() *protocol.InvokeOptions {
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
