package statestore

import (
	"context"
	"time"

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

const del = "DEL"

// Del deletes the given key. It returns the number of keys deleted
// (typically 0 or 1).
func (c *Client[K, V]) Del(
	ctx context.Context,
	key K,
	opt ...DelOption,
) (*Response[int], error) {
	if len(key) == 0 {
		return nil, ArgumentError{Name: "key"}
	}

	var opts DelOptions
	opts.Apply(opt)

	return invoke(ctx, c.invoker, resp.Number, &opts, resp.OpK(del, key))
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
