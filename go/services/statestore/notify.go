package statestore

import (
	"context"
	"encoding/hex"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

// Notify represents a notification event.
type Notify struct {
	Operation, Key string
	Value          []byte
}

// Notify messages for registered keys will be sent to this channel.
func (c *Client) Notify() <-chan Notify {
	c.notifyMu.RLock()
	defer c.notifyMu.RUnlock()
	return c.notify
}

// Receive a NOTIFY message.
func (c *Client) receive(
	ctx context.Context,
	msg *protocol.TelemetryMessage[[]byte],
) error {
	hexKey, ok := msg.TopicTokens["keyName"]
	if !ok {
		return resp.PayloadError("missing key name")
	}

	bytKey, err := hex.DecodeString(hexKey)
	if err != nil {
		return resp.PayloadError("invalid key name %q", hexKey)
	}

	data, err := resp.ParseBlobArray(msg.Payload)
	if err != nil {
		return err
	}

	opOnly := len(data) == 2
	hasValue := len(data) == 4

	if (!opOnly && !hasValue) ||
		(string(data[0]) != "NOTIFY") ||
		(hasValue && string(data[2]) != "VALUE") {
		return resp.PayloadError("invalid payload %q", string(msg.Payload))
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
