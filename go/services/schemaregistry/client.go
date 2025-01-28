// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import (
	"context"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/schemaregistry/dtmi_ms_adr_SchemaRegistry__1"
)

type (
	// Client represents a client of the schema registry.
	Client struct {
		client *dtmi_ms_adr_SchemaRegistry__1.SchemaRegistryClient
	}

	// ClientOption represents a single option for the client.
	ClientOption interface{ client(*ClientOptions) }

	// ClientOptions are the resolved options for the client.
	ClientOptions struct {
		Logger *slog.Logger
	}
)

// New creates a new schema registry client.
func New(
	app *protocol.Application,
	client protocol.MqttClient,
	opt ...ClientOption,
) (*Client, error) {
	var opts ClientOptions
	opts.Apply(opt)

	sr, err := dtmi_ms_adr_SchemaRegistry__1.NewSchemaRegistryClient(
		app,
		client,
		opts.invoker(),
		protocol.WithResponseTopicPrefix("clients/{invokerClientId}"),
	)
	if err != nil {
		return nil, err
	}
	return &Client{sr}, nil
}

// Start listening to all underlying MQTT topics.
func (c *Client) Start(ctx context.Context) error {
	return c.client.Start(ctx)
}

// Close all underlying MQTT topics and free resources.
func (c *Client) Close() {
	c.client.Close()
}

// Apply resolves the provided list of options.
func (o *ClientOptions) Apply(
	opts []ClientOption,
	rest ...ClientOption,
) {
	for opt := range options.Apply[ClientOption](opts, rest...) {
		opt.client(o)
	}
}

func (o *ClientOptions) client(opt *ClientOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o withLogger) client(opt *ClientOptions) {
	opt.Logger = o.Logger
}

func (o *ClientOptions) invoker() *protocol.CommandInvokerOptions {
	return &protocol.CommandInvokerOptions{
		Logger: o.Logger,
	}
}
