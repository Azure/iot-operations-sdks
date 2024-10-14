package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// GetOption represents a single option for the Get method.
	GetOption interface{ get(*GetOptions) }

	// GetOptions are the resolved options for the Get method.
	GetOptions struct {
		Timeout time.Duration
	}
)

const get = "GET"

// Get the value and version of the given key. If the key is not present, it
// returns a fully zero response struct; if the key is present but empty, it
// returns an empty value and the stored version.
func (c *Client[K, V]) Get(
	ctx context.Context,
	key K,
	opt ...GetOption,
) (*Response[V], error) {
	if len(key) == 0 {
		return nil, ArgumentError{Name: "key"}
	}

	var opts GetOptions
	opts.Apply(opt)

	return invoke(ctx, c.invoker, resp.Blob[V], &opts, resp.OpK(get, key))
}

// Apply resolves the provided list of options.
func (o *GetOptions) Apply(opts []GetOption, rest ...GetOption) {
	for opt := range options.Apply[GetOption](opts, rest...) {
		opt.get(o)
	}
}

func (o *GetOptions) get(opt *GetOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithTimeout) get(opt *GetOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *GetOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
	}
}
