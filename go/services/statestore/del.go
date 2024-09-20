package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
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

// Del deletes the value of the given key. If the key was present, it returns
// true and the stored version of the key; otherwise, it returns false and a
// zero version.
func (c *Client) Del(
	ctx context.Context,
	key string,
	opt ...DelOption,
) (*Response[bool], error) {
	var opts DelOptions
	opts.Apply(opt)
	return invoke(ctx, c.invoker, parseBool, &opts, "DEL", key)
}

// Apply resolves the provided list of options.
func (o *DelOptions) Apply(
	opts []DelOption,
	rest ...DelOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.del(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.del(o)
		}
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
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
		FencingToken:  o.FencingToken,
	}
}
