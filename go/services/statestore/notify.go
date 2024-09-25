package statestore

import (
	"context"
	"encoding/hex"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
)

// Notify represents a notification event.
type Notify struct {
	Key       string
	Operation string
	Value     []byte
}

// Receive a NOTIFY message.
func (c *Client) notifyReceive(
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

	key := string(bytKey)
	op := string(data[1])
	var val []byte
	if hasValue {
		val = data[3]
	}

	// TODO: Lock less globally if possible, but keep it simple for now.
	c.notifyMu.RLock()
	defer c.notifyMu.RUnlock()

	for _, kn := range c.notify[key] {
		select {
		case kn.c <- Notify{key, op, val}:
		case <-ctx.Done():
		}
	}

	return nil
}
