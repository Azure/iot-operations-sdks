package statestore

import (
	"context"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// Bytes represents generic byte data.
	Bytes interface{ ~string | ~[]byte }

	// Client represents a client of the state store.
	Client[K, V Bytes] struct {
		invoker *protocol.CommandInvoker[[]byte, []byte]
	}

	// ClientOption represents a single option for the client.
	ClientOption interface{ client(*ClientOptions) }

	// ClientOptions are the resolved options for the client.
	ClientOptions struct {
		Logger *slog.Logger
	}

	// Response represents a state store response, which will include a value
	// depending on the method and the stored version returned for the key
	// (if any).
	Response[T any] struct {
		Value   T
		Version hlc.HybridLogicalClock
	}

	ServiceError  = errors.Service
	PayloadError  = errors.Payload
	ArgumentError = errors.Argument

	// This option is not used directly; see WithLogger below.
	withLogger struct{ *slog.Logger }
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
	client mqtt.Client,
	opt ...ClientOption,
) (*Client[K, V], error) {
	c := &Client[K, V]{}
	var err error

	var opts ClientOptions
	opts.Apply(opt)

	c.invoker, err = protocol.NewCommandInvoker(
		client,
		protocol.Raw{},
		protocol.Raw{},
		"statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke",
		opts.invoker(),
		protocol.WithResponseTopicPrefix("clients/{clientId}"),
		protocol.WithResponseTopicSuffix("response"),
		protocol.WithTopicTokens{"clientId": client.ClientID()},
	)
	if err != nil {
		return nil, err
	}

	return c, nil
}

// Listen to the response topic(s). Returns a function to stop listening. Must
// be called before any state store methods. Note that cancelling this context
// will cause the unsubscribe call to fail.
func (c *Client[K, V]) Listen(ctx context.Context) (func(), error) {
	return c.invoker.Listen(ctx)
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func([]byte) (T, error),
	opts invokeOptions,
	data []byte,
) (*Response[T], error) {
	res, err := invoker.Invoke(ctx, data, opts.invoke())
	if err != nil {
		return nil, err
	}

	val, err := parse(res.Payload)
	if err != nil {
		return nil, err
	}

	return &Response[T]{val, res.Timestamp}, nil
}

// Shorthand to check an "OK" response.
func parseOK(data []byte) (bool, error) {
	switch data[0] {
	// SET and KEYNOTIFY return +OK on success.
	case '+':
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

// Apply resolves the provided list of options.
func (o *ClientOptions) Apply(
	opts []ClientOption,
	rest ...ClientOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.client(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.client(o)
		}
	}
}

func (o *ClientOptions) client(opt *ClientOptions) {
	if o != nil {
		*opt = *o
	}
}

// WithLogger enables logging with the provided slog logger.
func WithLogger(logger *slog.Logger) ClientOption {
	return withLogger{logger}
}

func (o withLogger) client(opt *ClientOptions) {
	opt.Logger = o.Logger
}

func (o *ClientOptions) invoker() *protocol.CommandInvokerOptions {
	return &protocol.CommandInvokerOptions{
		Logger: o.Logger,
	}
}
