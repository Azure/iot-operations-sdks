// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package statestore

import (
	"context"
	"encoding/hex"
	"log/slog"
	"strings"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

const fencingToken = "__ft"

type (
	// Bytes represents generic byte data.
	Bytes interface{ ~string | ~[]byte }

	// Client represents a client of the state store.
	Client[K, V Bytes] struct {
		client    protocol.MqttClient
		listeners protocol.Listeners
		log       log.Logger
		done      func()

		invoker  *protocol.CommandInvoker[[]byte, []byte]
		receiver *protocol.TelemetryReceiver[[]byte]

		notify   map[string]map[chan Notify[K, V]]chan struct{}
		notifyMu sync.RWMutex

		keynotify   map[string]uint
		keynotifyMu sync.RWMutex

		manualAck bool
	}

	// ClientOption represents a single option for the client.
	ClientOption interface{ client(*ClientOptions) }

	// ClientOptions are the resolved options for the client.
	ClientOptions struct {
		Concurrency uint
		ManualAck   bool
		Logger      *slog.Logger
	}

	// Response represents a state store response, which will include a value
	// depending on the method and the stored version returned for the key
	// (if any).
	Response[T any] struct {
		Value   T
		Version hlc.HybridLogicalClock
	}

	// ServiceError indicates an error returned from the state store.
	ServiceError = errors.Service
	// PayloadError indicates a malformed or unexpected payload returned from
	// the state store.
	PayloadError = errors.Payload
	// ArgumentError indicates an invalid argument.
	ArgumentError = errors.Argument

	MqttClient interface {
		protocol.MqttClient
		RegisterConnectEventHandler(mqtt.ConnectEventHandler) func()
	}
)

var (
	ErrService  = errors.ErrService
	ErrPayload  = errors.ErrPayload
	ErrArgument = errors.ErrArgument
)

// New creates a new state store client. It takes the key and value types as
// parameters to avoid unnecessary casting; both may be string, []byte, or
// equivalent types.
func New[K, V Bytes](
	app *protocol.Application,
	client MqttClient,
	opt ...ClientOption,
) (*Client[K, V], error) {
	c := &Client[K, V]{
		client:    client,
		notify:    map[string]map[chan Notify[K, V]]chan struct{}{},
		keynotify: map[string]uint{},
	}
	var err error

	var opts ClientOptions
	opts.Apply(opt)
	c.log = log.Wrap(opts.Logger)

	c.manualAck = opts.ManualAck

	tokens := protocol.WithTopicTokens{
		"clientId": strings.ToUpper(hex.EncodeToString([]byte(client.ID()))),
	}

	c.invoker, err = protocol.NewCommandInvoker(
		app,
		client,
		protocol.Raw{},
		protocol.Raw{},
		"statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke",
		opts.invoker(),
		protocol.WithResponseTopicPrefix("clients/{clientId}"),
		protocol.WithResponseTopicSuffix("response"),
		tokens,
	)
	ctx := context.Background()
	if err != nil {
		c.log.Warn(ctx, err)
		c.listeners.Close()
		return nil, err
	}
	c.listeners = append(c.listeners, c.invoker)

	c.receiver, err = protocol.NewTelemetryReceiver(
		app,
		client,
		protocol.Raw{},
		"clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{clientId}/command/notify/{keyName}",
		c.notifyReceive,
		opts.receiver(),
		tokens,
	)
	if err != nil {
		c.log.Warn(ctx, err)
		c.listeners.Close()
		return nil, err
	}
	c.listeners = append(c.listeners, c.invoker)

	ctx, cancel := context.WithCancel(ctx)
	done := client.RegisterConnectEventHandler(func(*mqtt.ConnectEvent) {
		c.reconnect(ctx)
	})
	c.done = func() {
		done()
		cancel()
	}

	return c, nil
}

// Start listening to all underlying MQTT topics.
func (c *Client[K, V]) Start(ctx context.Context) error {
	err := c.listeners.Start(ctx)
	if err != nil {
		c.log.Warn(ctx, err)
	}
	return nil
}

// Close all underlying MQTT topics and free resources.
func (c *Client[K, V]) Close() {
	ctx := context.Background()
	c.log.Info(ctx, "shutting down state store client")
	c.done()
	c.listeners.Close()
	c.log.Info(ctx, "state store client shutdown complete")
}

// ID returns the ID of the underlying MQTT client.
func (c *Client[K, V]) ID() string {
	return c.client.ID()
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func([]byte) (T, error),
	opts invokeOptions,
	data []byte,
	logger log.Logger,
) (*Response[T], error) {
	res, err := invoker.Invoke(ctx, data, opts.invoke())
	if err != nil {
		logger.Error(ctx, err)
		return nil, err
	}

	val, err := parse(res.Payload)
	if err != nil {
		logger.Error(ctx, err)
		return nil, err
	}

	return &Response[T]{val, res.Timestamp}, nil
}

// Shorthand to check an "OK" response.
func parseOK(data []byte) (bool, error) {
	switch data[0] {
	// SET and KEYNOTIFY return +OK on success.
	case '+', '-':
		res, err := resp.String(data)
		if err != nil {
			return false, err
		}
		if res != "OK" {
			return false, resp.PayloadError("unexpected response %q", res)
		}
		return true, nil

	// SET returns :-1 if it is skipped due to NX or NEX. KEYNOTIFY returns :0
	// if set on an existing key.
	case ':':
		res, err := resp.Number(data)
		if err != nil {
			return false, err
		}
		if res > 0 {
			return false, resp.PayloadError("unexpected response %d", res)
		}
		return false, nil

	default:
		return false, resp.PayloadError("wrong type %q", data[0])
	}
}

func (c *Client[K, V]) logK(
	ctx context.Context,
	operation string,
	key K,
	attrs ...slog.Attr,
) {
	if c.log.Enabled(ctx, slog.LevelDebug) {
		all := []slog.Attr{
			slog.String("key", string(key)),
		}
		if len(attrs) > 0 {
			all = append(all, attrs...)
		}
		c.log.Debug(ctx, operation, all...)
	}
}

func (c *Client[K, V]) logKV(
	ctx context.Context,
	operation string,
	key K,
	value V,
	attrs ...slog.Attr,
) {
	if c.log.Enabled(ctx, slog.LevelDebug) {
		all := []slog.Attr{
			slog.String("key", string(key)),
			slog.String("value", string(value)),
		}
		if len(attrs) > 0 {
			all = append(all, attrs...)
		}
		c.log.Debug(ctx, operation, all...)
	}
}

func (c *Client[K, V]) validateKey(ctx context.Context, key K) error {
	if len(key) == 0 {
		errArg := ArgumentError{Name: "key"}
		c.log.Error(ctx, errArg)
		return errArg
	}
	return nil
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

func (o WithConcurrency) client(opt *ClientOptions) {
	opt.Concurrency = uint(o)
}

func (o WithManualAck) client(opt *ClientOptions) {
	opt.ManualAck = bool(o)
}

func (o withLogger) client(opt *ClientOptions) {
	opt.Logger = o.Logger
}

func (o *ClientOptions) invoker() *protocol.CommandInvokerOptions {
	return &protocol.CommandInvokerOptions{
		Logger: o.Logger,
	}
}

func (o *ClientOptions) receiver() *protocol.TelemetryReceiverOptions {
	return &protocol.TelemetryReceiverOptions{
		Concurrency: o.Concurrency,
		ManualAck:   o.ManualAck,
		Logger:      o.Logger,
	}
}
