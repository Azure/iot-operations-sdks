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
	// PutOption represents a single option for the Put method.
	PutOption interface{ put(*PutOptions) }

	// PutOptions are the resolved options for the Put method.
	PutOptions struct {
		SchemaType SchemaType
		Tags       map[string]string
		Timeout    time.Duration
		Version    string
	}
)

// Put adds or updates a schema in the schema registry.
func (c *Client) Put(
	ctx context.Context,
	content string,
	format Format,
	opt ...PutOption,
) (*Schema, error) {
	var opts PutOptions
	opts.Apply(opt)

	if opts.Version == "" {
		opts.Version = "1.0.0"
	}

	req := schemaregistry.PutRequestSchema{
		SchemaContent: &content,
		Format:        &format,
		Tags:          opts.Tags,
		Version:       &opts.Version,
	}

	res, err := c.client.Put(
		ctx,
		schemaregistry.PutRequestPayload{PutSchemaRequest: req},
		opts.invoke(),
		protocol.WithMetadata{"__invId": c.invID},
	)
	if err != nil {
		return nil, err
	}
	return &res.Payload.Schema, nil
}

// Apply resolves the provided list of options.
func (o *PutOptions) Apply(opts []PutOption, rest ...PutOption) {
	for opt := range options.Apply[PutOption](opts, rest...) {
		opt.put(o)
	}
}

func (o *PutOptions) put(opt *PutOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithTimeout) put(opt *PutOptions) {
	opt.Timeout = time.Duration(o)
}

func (o WithVersion) put(opt *PutOptions) {
	opt.Version = string(o)
}

func (o *PutOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		Timeout: o.Timeout,
	}
}
