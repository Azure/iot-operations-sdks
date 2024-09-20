package statestore

import (
	"context"
	"strconv"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// Client represents a client of the state store.
	Client struct {
		invoker *protocol.CommandInvoker[[]byte, []byte]
	}

	// Response represents a state store response, which will include a value
	// depending on the method and the stored version returned for the key
	// (if any).
	Response[T any] struct {
		Value   T
		Version hlc.HybridLogicalClock
	}

	ResponseError = errors.Response
	PayloadError  = errors.Payload
	ArgumentError = errors.Argument
)

var (
	ErrResponse = errors.ErrResponse
	ErrPayload  = errors.ErrPayload
	ErrArgument = errors.ErrArgument
)

// New creates a new state store client.
func New(client mqtt.Client) (*Client, error) {
	c := &Client{}
	var err error

	c.invoker, err = protocol.NewCommandInvoker(
		client,
		protocol.Raw{},
		protocol.Raw{},
		"statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke",
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
func (c *Client) Listen(ctx context.Context) (func(), error) {
	return c.invoker.Listen(ctx)
}

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

	var invOpt []protocol.InvokeOption
	if !opts.FencingToken.IsZero() {
		invOpt = []protocol.InvokeOption{
			protocol.WithFencingToken(opts.FencingToken),
		}
	}

	return invoke(ctx, c.invoker, parseOK, args, invOpt...)
}

// Get the value and version of the given key. If the key is not present, it
// returns nil and a zero version; if the key is present but empty, it returns
// an empty slice and the stored version.
func (c *Client) Get(
	ctx context.Context,
	key string,
) (*Response[[]byte], error) {
	return invoke(ctx, c.invoker, resp.ParseBlob, []string{"GET", key})
}

// Del deletes the value of the given key. If the key was present, it returns
// true and the stored version of the key; otherwise, it returns false and a
// zero version.
func (c *Client) Del(
	ctx context.Context,
	key string,
) (*Response[bool], error) {
	return invoke(ctx, c.invoker, parseBool, []string{"DEL", key})
}

// Vdel deletes the value of the given key if it is equal to the given value.
// If the key was present and the value matched, it returns true and the stored
// version of the key; otherwise, it returns false and a zero version.
func (c *Client) Vdel(
	ctx context.Context,
	key string,
	val []byte,
) (*Response[bool], error) {
	return invoke(ctx, c.invoker, parseBool, []string{"VDEL", key, string(val)})
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func([]byte) (T, error),
	args []string,
	opts ...protocol.InvokeOption,
) (*Response[T], error) {
	if args[1] == "" {
		return nil, ArgumentError{Name: "key"}
	}

	res, err := invoker.Invoke(ctx, resp.FormatBlobArray(args...), opts...)
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
		res, err := resp.ParseString(data)
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
		res, err := resp.ParseNumber(data)
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

// Shorthand to check a "boolean" numeric response.
func parseBool(data []byte) (bool, error) {
	res, err := resp.ParseNumber(data)
	return err == nil && res > 0, err
}
