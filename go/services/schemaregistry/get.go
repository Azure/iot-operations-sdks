// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/schemaregistry/schemaregistry"
)

type (
	// GetOption represents a single option for the Get method.
	GetOption interface{ get(*GetOptions) }

	// GetOptions are the resolved options for the Get method.
	GetOptions struct {
		Timeout time.Duration
		Version string
	}
)

// Get retrieves schema information from the schema registry.
func (c *Client) Get(
	ctx context.Context,
	name string,
	opt ...GetOption,
) (*Schema, error) {
	var opts GetOptions
	opts.Apply(opt)

	if opts.Version == "" {
		opts.Version = "1.0.0"
	}

	req := schemaregistry.GetRequestSchema{
		Name:    &name,
		Version: &opts.Version,
	}

	res, err := c.client.Get(
		ctx,
		schemaregistry.GetRequestPayload{GetSchemaRequest: req},
		opts.invoke(),
		protocol.WithMetadata{"__invId": c.invID},
	)
	if err != nil {
		return nil, translateError(err)
	}
	return res.Payload.Schema, nil
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

func (o WithVersion) get(opt *GetOptions) {
	opt.Version = string(o)
}

func (o *GetOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		Timeout: o.Timeout,
	}
}
