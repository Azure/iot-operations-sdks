package statestore

import (
	"context"
	"encoding/hex"
	"strings"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

type (
	// Client represents a client of the state store.
	Client struct {
		invoker  *protocol.CommandInvoker[[]byte, []byte]
		receiver *protocol.TelemetryReceiver[[]byte]
		notify   chan Notify
		notifyMu sync.RWMutex
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
func New(client mqtt.Client, opt ...protocol.Option) (*Client, error) {
	c := &Client{}
	var err error

	tokens := protocol.WithTopicTokens{
		"clientId": strings.ToUpper(
			hex.EncodeToString([]byte(client.ClientID())),
		),
	}

	var invOpt protocol.CommandInvokerOptions
	invOpt.ApplyOptions(opt)

	c.invoker, err = protocol.NewCommandInvoker(
		client,
		protocol.Raw{},
		protocol.Raw{},
		"statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke",
		&invOpt,
		protocol.WithResponseTopicPrefix("clients/{clientId}"),
		protocol.WithResponseTopicSuffix("response"),
		tokens,
	)
	if err != nil {
		return nil, err
	}

	var recOpt protocol.TelemetryReceiverOptions
	recOpt.ApplyOptions(opt)

	c.receiver, err = protocol.NewTelemetryReceiver(
		client,
		protocol.Raw{},
		"clients/statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/{clientId}/command/notify/{keyName}",
		c.receive,
		&recOpt,
		tokens,
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
	done, err := protocol.Listen(ctx, c.invoker, c.receiver)
	if err != nil {
		return nil, err
	}

	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()
	c.notify = make(chan Notify)

	return func() {
		done()
		close(c.notify)

		c.notifyMu.Lock()
		defer c.notifyMu.Unlock()
		c.notify = nil
	}, nil
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func([]byte) (T, error),
	opts invokeOptions,
	args ...string,
) (*Response[T], error) {
	if args[1] == "" {
		return nil, ArgumentError{Name: "key"}
	}

	res, err := invoker.Invoke(
		ctx,
		resp.FormatBlobArray(args...),
		opts.invoke(),
	)
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
