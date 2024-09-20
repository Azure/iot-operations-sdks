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
// true and the new or updated timestamp; if the key is not set due to the
// specified condition, it returns false and the stored timestamp.
func (c *Client) Set(
	ctx context.Context,
	key string,
	val []byte,
	opt ...SetOption,
) (bool, hlc.HybridLogicalClock, error) {
	var opts SetOptions
	opts.Apply(opt)

	args := []string{"SET", key, string(val)}

	switch opts.Condition {
	case Always:
		// No-op.
	case NotExists:
		args = append(args, "NX")
	case NotExistsOrEqual:
		args = append(args, "NEX")
	default:
		return false, hlc.HybridLogicalClock{},
			ArgumentError{Name: "Condition", Value: opts.Condition}
	}

	switch {
	case opts.Expiry < 0:
		return false, hlc.HybridLogicalClock{},
			ArgumentError{Name: "Expiry", Value: opts.Expiry}
	case opts.Expiry > 0:
		exp := strconv.Itoa(int(opts.Expiry.Milliseconds()))
		args = append(args, "PX", exp)
	}

	return invoke(ctx, c.invoker, parseOK, args...)
}

// Get the value and timestamp of the given key. If the key is not present, it
// returns nil and a zero timestamp; if the key is present but empty, it returns
// an empty slice and the stored timestamp.
func (c *Client) Get(
	ctx context.Context,
	key string,
) ([]byte, hlc.HybridLogicalClock, error) {
	return invoke(ctx, c.invoker, resp.ParseBlob, "GET", key)
}

// Del deletes the value of the given key. If the key was present, it returns
// true and the stored timestamp of the key; otherwise, it returns false and a
// zero timestamp.
func (c *Client) Del(
	ctx context.Context,
	key string,
) (bool, hlc.HybridLogicalClock, error) {
	return invoke(ctx, c.invoker, parseBool, "DEL", key)
}

// Vdel deletes the value of the given key if it is equal to the given value.
// If the key was present and the value matched, it returns true and the stored
// timestamp of the key; otherwise, it returns false and a zero timestamp.
func (c *Client) Vdel(
	ctx context.Context,
	key string,
	val []byte,
) (bool, hlc.HybridLogicalClock, error) {
	return invoke(ctx, c.invoker, parseBool, "VDEL", key, string(val))
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func([]byte) (T, error),
	args ...string,
) (T, hlc.HybridLogicalClock, error) {
	var zero T
	if args[1] == "" {
		return zero, hlc.HybridLogicalClock{}, ArgumentError{Name: "key"}
	}

	res, err := invoker.Invoke(ctx, resp.FormatBlobArray(args...))
	if err != nil {
		return zero, hlc.HybridLogicalClock{}, err
	}

	val, err := parse(res.Payload)
	if err != nil {
		return zero, hlc.HybridLogicalClock{}, err
	}

	return val, res.Timestamp, nil
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
