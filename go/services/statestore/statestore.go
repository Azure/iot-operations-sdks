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
	// Client represents a client of the MQ state store.
	Client struct {
		invoker  *protocol.CommandInvoker[[]byte, []byte]
		receiver *protocol.TelemetryReceiver[[]byte]
		notify   chan Notify
		notifyMu sync.RWMutex
	}

	// NotifyHandler processes a notification event.
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

	return invokeOK(ctx, c.invoker, args...)
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

// KeyNotify requests or stops notification for a key.
func (c *Client) KeyNotify(ctx context.Context, key string, notify bool) error {
	args := []string{"KEYNOTIFY", key}
	if !notify {
		args = append(args, "STOP")
	}
	return invokeOK(ctx, c.invoker, args...)
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

// Shorthand to invoke with an expected "OK" response.
func invokeOK(
	ctx context.Context,
	invoker *protocol.CommandInvoker[[]byte, []byte],
	args ...string,
) error {
	res, err := invoke(ctx, invoker, resp.ParseString, args...)
	if err != nil {
		return err
	}
	if res != "OK" {
		return &Error{
			Operation: args[0],
			Message:   "unexpected response",
			Value:     res,
		}
	}
	return nil
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

	body, err := resp.ParseBlobArray("NOTIFY", msg.Payload)
	if err != nil {
		return err
	}

	key := string(bytKey)
	var op string
	var val []byte
	switch len(body) {
	case 2:
		op = string(body[1])
	case 4:
		op = string(body[1])
		val = body[3]
	default:
		return &Error{
			Operation: "NOTIFY",
			Message:   "invalid payload",
			Value:     string(msg.Payload),
		}
	}

	select {
	case c.notify <- Notify{op, key, val}:
	case <-ctx.Done():
	}
	return nil
}
