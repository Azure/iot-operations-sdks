package statestore

import (
	"context"
	"strconv"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// Client represents a client of the MQ state store.
	Client struct {
		invoker *protocol.CommandInvoker[[]byte, []byte]
	}

	// Error represents an error in a state store method.
	Error = internal.Error
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

// Set the value of the given key.
func (c *Client) Set(
	ctx context.Context,
	key string,
	val []byte,
	opt ...SetOption,
) error {
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
		return &Error{
			Operation: "SET",
			Message:   "invalid condition",
			Value:     strconv.Itoa(int(opts.Condition)),
		}
	}

	switch {
	case opts.Expiry < 0:
		return &Error{
			Operation: "SET",
			Message:   "negative expiry",
			Value:     opts.Expiry.String(),
		}
	case opts.Expiry > 0:
		exp := strconv.Itoa(int(opts.Expiry.Milliseconds()))
		args = append(args, "PX", exp)
	}

	res, err := invoke(ctx, c.invoker, resp.ParseString, args...)
	if err != nil {
		return err
	}
	if res != "OK" {
		return &Error{
			Operation: "SET",
			Message:   "unexpected response",
			Value:     res,
		}
	}
	return nil
}

// Get the value of the given key.
func (c *Client) Get(ctx context.Context, key string) ([]byte, error) {
	return invoke(ctx, c.invoker, resp.ParseBlob, "GET", key)
}

// Del deletes the value of the given key. Returns whether a value was deleted.
func (c *Client) Del(ctx context.Context, key string) (bool, error) {
	n, err := invoke(ctx, c.invoker, resp.ParseNumber, "DEL", key)
	return err == nil && n > 0, err
}

// Vdel deletes the value of the given key if it is equal to the given value.
// Returns whether a value was deleted.
func (c *Client) Vdel(
	ctx context.Context,
	key string,
	val []byte,
) (bool, error) {
	n, err := invoke(ctx, c.invoker, resp.ParseNumber, "VDEL", key, string(val))
	return err == nil && n > 0, err
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func(cmd string, byt []byte) (T, error),
	args ...string,
) (T, error) {
	var zero T
	res, err := invoker.Invoke(ctx, resp.FormatBlobArray(args...))
	if err != nil {
		return zero, err
	}
	return parse(args[0], res.Payload)
}
