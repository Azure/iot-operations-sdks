// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package schemaregistry

import (
	"context"
	"encoding/json"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/services/schemaregistry/schemaregistry"
)

type (
	// Client represents a client of the schema registry.
	Client struct {
		client *schemaregistry.SchemaRegistryClient

		// TODO: Remove when no longer necessary for compat.
		invID string
	}

	// ClientOption represents a single option for the client.
	ClientOption interface{ client(*ClientOptions) }

	// ClientOptions are the resolved options for the client.
	ClientOptions struct {
		Logger *slog.Logger
	}

	// Error represents an error returned by the schema registry.
	Error struct {
		Message       string
		PropertyName  string
		PropertyValue any
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

	sr, err := schemaregistry.NewSchemaRegistryClient(
		app,
		client,
		opts.invoker(),
	)
	if err != nil {
		return nil, err
	}
	return &Client{sr, client.ID()}, nil
}

// Start listening to all underlying MQTT topics.
func (c *Client) Start(ctx context.Context) error {
	return c.client.Start(ctx)
}

// Close all underlying MQTT topics and free resources.
func (c *Client) Close() {
	c.client.Close()
}

// Error returns the error message.
func (e *Error) Error() string {
	return e.Message
}

//nolint:staticcheck // schemaregistry compat.
func translateError(err error) error {
	switch e := err.(type) {
	case *errors.Remote:
		if k, ok := e.Kind.(errors.UnknownError); ok && k.PropertyName != "" {
			return &Error{
				Message:       err.Error(),
				PropertyName:  k.PropertyName,
				PropertyValue: k.PropertyValue,
			}
		}

	case *errors.Client:
		if _, ok := e.Kind.(errors.PayloadInvalid); ok {
			if j, ok := e.Nested.(*json.SyntaxError); ok && j.Offset == 0 {
				// We're already returning a nil schema (because of the error),
				// so just treat the 404 case as not an error.
				return nil
			}
		}
	}
	return err
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
