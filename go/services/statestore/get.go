package statestore

import (
	"context"
	"time"

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

// Get the value and version of the given key. If the key is not present, it
// returns nil and a zero version; if the key is present but empty, it returns
// an empty slice and the stored version.
func (c *Client) Get(
	ctx context.Context,
	key string,
	opt ...GetOption,
) (*Response[[]byte], error) {
	var opts GetOptions
	opts.Apply(opt)
	return invoke(ctx, c.invoker, resp.ParseBlob, &opts, "GET", key)
}

// Apply resolves the provided list of options.
func (o *GetOptions) Apply(
	opts []GetOption,
	rest ...GetOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.get(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.get(o)
		}
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
