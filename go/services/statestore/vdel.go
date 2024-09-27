package statestore

import (
	"context"
	"time"

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

// VDel deletes the value of the given key if it is equal to the given value.
// It returns the number of values deleted.
func (c *Client) VDel(
	ctx context.Context,
	key string,
	val []byte,
	opt ...VDelOption,
) (*Response[int], error) {
	var opts VDelOptions
	opts.Apply(opt)
	return invoke(
		ctx,
		c.invoker,
		resp.ParseNumber,
		&opts,
		"VDEL",
		key,
		string(val),
	)
}

// Apply resolves the provided list of options.
func (o *VDelOptions) Apply(
	opts []VDelOption,
	rest ...VDelOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.vdel(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.vdel(o)
		}
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
