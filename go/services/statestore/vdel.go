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
	// VDelOption represents a single option for the VDel method.
	VDelOption interface{ vdel(*VDelOptions) }

	// VDelOptions are the resolved options for the VDel method.
	VDelOptions struct {
		FencingToken hlc.HybridLogicalClock
		Timeout      time.Duration
	}
)

const vdel = "VDEL"

// VDel deletes the given key if it is equal to the given value. It returns the
// number of values deleted (typically 0 or 1) or -1 if the key was present but
// did not match the given value.
func (c *Client[K, V]) VDel(
	ctx context.Context,
	key K,
	val V,
	opt ...VDelOption,
) (*Response[int], error) {
	if len(key) == 0 {
		return nil, ArgumentError{Name: "key"}
	}

	var opts VDelOptions
	opts.Apply(opt)

	return invoke(ctx, c.invoker, resp.Number, &opts, resp.OpKV(vdel, key, val))
}

// Apply resolves the provided list of options.
func (o *VDelOptions) Apply(opts []VDelOption, rest ...VDelOption) {
	for opt := range options.Apply[VDelOption](opts, rest...) {
		opt.vdel(o)
	}
}

func (o *VDelOptions) vdel(opt *VDelOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithFencingToken) vdel(opt *VDelOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}

func (o WithTimeout) vdel(opt *VDelOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *VDelOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
		FencingToken:  o.FencingToken,
	}
}
