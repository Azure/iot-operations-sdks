package statestore

import (
	"context"
	"strconv"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
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
func (c *Client) Set(
	ctx context.Context,
	key string,
	val []byte,
	opt ...SetOption,
) (*Response[bool], error) {
	var opts SetOptions
	opts.Apply(opt)

	args := []string{"SET", key, string(val)}

	if opts.Condition != Always {
		args = append(args, string(opts.Condition))
	}

	switch {
	case opts.Expiry < 0:
		return nil, ArgumentError{Name: "Expiry", Value: opts.Expiry}
	case opts.Expiry > 0:
		exp := strconv.Itoa(int(opts.Expiry.Milliseconds()))
		args = append(args, "PX", exp)
	}

	return invoke(ctx, c.invoker, parseOK, &opts, args...)
}

// Apply resolves the provided list of options.
func (o *SetOptions) Apply(
	opts []SetOption,
	rest ...SetOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.set(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.set(o)
		}
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
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
		FencingToken:  o.FencingToken,
	}
}
