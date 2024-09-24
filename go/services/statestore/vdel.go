package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// VdelOption represents a single option for the Vdel method.
	VdelOption interface{ vdel(*VdelOptions) }

	// VdelOptions are the resolved options for the Vdel method.
	VdelOptions struct {
		FencingToken hlc.HybridLogicalClock
		Timeout      time.Duration
	}
)

// Vdel deletes the value of the given key if it is equal to the given value.
// If the key was present and the value matched, it returns true and the stored
// version of the key; otherwise, it returns false and a zero version.
func (c *Client) Vdel(
	ctx context.Context,
	key string,
	val []byte,
	opt ...VdelOption,
) (*Response[bool], error) {
	var opts VdelOptions
	opts.Apply(opt)
	return invoke(ctx, c.invoker, parseBool, &opts, "VDEL", key, string(val))
}

// Apply resolves the provided list of options.
func (o *VdelOptions) Apply(
	opts []VdelOption,
	rest ...VdelOption,
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

func (o *VdelOptions) vdel(opt *VdelOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithFencingToken) vdel(opt *VdelOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}

func (o WithTimeout) vdel(opt *VdelOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *VdelOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
		FencingToken:  o.FencingToken,
	}
}
