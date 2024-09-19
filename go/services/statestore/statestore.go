package statestore

import (
	"context"
	"encoding/hex"
	"strconv"
	"strings"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal"
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

	// Notify represents a notification event.
	Notify struct {
		Operation, Key string
		Value          []byte
	}

	// Error represents an error in a state store method.
	Error = internal.Error
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

	_, err := invoke(ctx, c.invoker, parseOK, args...)
	return err
}

// Get the value of the given key.
func (c *Client) Get(ctx context.Context, key string) ([]byte, error) {
	return invoke(ctx, c.invoker, resp.ParseBlob, "GET", key)
}

// Del deletes the value of the given key. If the key was not present, returns
// false with no error.
func (c *Client) Del(ctx context.Context, key string) (bool, error) {
	return invoke(ctx, c.invoker, parseBool, "DEL", key)
}

// Vdel deletes the value of the given key if it is equal to the given value.
// If the key was not present or the value did not match, returns false with no
// error.
func (c *Client) Vdel(
	ctx context.Context,
	key string,
	val []byte,
) (bool, error) {
	return invoke(ctx, c.invoker, parseBool, "VDEL", key, string(val))
}

// KeyNotify requests or stops notification for a key. If a stop is requested on
// a key that did not have notifications, it will return false with no error.
func (c *Client) KeyNotify(
	ctx context.Context,
	key string,
	notify bool,
) (bool, error) {
	args := []string{"KEYNOTIFY", key}
	if !notify {
		args = append(args, "STOP")
	}
	res, err := c.invoker.Invoke(ctx, resp.FormatBlobArray(args...))
	if err != nil {
		return false, err
	}
	switch res.Payload[0] {
	case '+':
		return parseOK("KEYNOTIFY", res.Payload)
	case ':':
		return parseBool("KEYNOTIFY", res.Payload)
	default:
		return false, resp.ErrWrongType("KEYNOTIFY", res.Payload[0])
	}
}

// Notify messages for registered keys will be sent to this channel.
func (c *Client) Notify() <-chan Notify {
	c.notifyMu.RLock()
	defer c.notifyMu.RUnlock()
	return c.notify
}

// Shorthand to invoke and parse.
func invoke[T any](
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	parse func(string, []byte) (T, error),
	args ...string,
) (T, error) {
	var zero T
	res, err := invoker.Invoke(ctx, resp.FormatBlobArray(args...))
	if err != nil {
		return zero, err
	}
	return parse(args[0], res.Payload)
}

// Shorthand to check an "OK" response.
func parseOK(op string, data []byte) (bool, error) {
	res, err := resp.ParseString(op, data)
	if err != nil {
		return false, err
	}
	if res != "OK" {
		return false, &Error{
			Operation: op,
			Message:   "unexpected response",
			Value:     res,
		}
	}
	return true, nil
}

// Shorthand to check a "boolean" numeric response.
func parseBool(op string, data []byte) (bool, error) {
	res, err := resp.ParseNumber(op, data)
	return err == nil && res > 0, err
}

// Receive a NOTIFY message.
func (c *Client) receive(
	ctx context.Context,
	msg *protocol.TelemetryMessage[[]byte],
) error {
	hexKey, ok := msg.TopicTokens["keyName"]
	if !ok {
		return &Error{
			Operation: "NOTIFY",
			Message:   "missing key name",
		}
	}

	bytKey, err := hex.DecodeString(hexKey)
	if err != nil {
		return &Error{
			Operation: "NOTIFY",
			Message:   "invalid key name",
			Value:     hexKey,
		}
	}

	data, err := resp.ParseBlobArray("NOTIFY", msg.Payload)
	if err != nil {
		return err
	}

	opOnly := len(data) == 2
	hasValue := len(data) == 4

	if (!opOnly && !hasValue) ||
		(string(data[0]) != "NOTIFY") ||
		(hasValue && string(data[2]) != "VALUE") {
		return &Error{
			Operation: "NOTIFY",
			Message:   "invalid payload",
			Value:     string(msg.Payload),
		}
	}

	var val []byte
	if hasValue {
		val = data[3]
	}

	select {
	case c.notify <- Notify{string(data[1]), string(bytKey), val}:
	case <-ctx.Done():
	}
	return nil
}
