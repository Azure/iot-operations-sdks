package protocol_test

import (
	"context"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/stretchr/testify/require"
)

// Simple happy-path sanity check.
func TestTelemetry(t *testing.T) {
	ctx := context.Background()
	stub := setupMqtt(ctx, t, 1885)
	defer stub.Broker.Close()

	enc := protocol.JSON[string]{}
	topic := "prefix/{token}/suffix"
	value := "test"

	results := make(chan *protocol.TelemetryMessage[string])

	receiver, err := protocol.NewTelemetryReceiver(stub.Server, enc, topic,
		func(_ context.Context, tm *protocol.TelemetryMessage[string]) error {
			results <- tm
			return nil
		},
	)
	require.NoError(t, err)

	sender, err := protocol.NewTelemetrySender(stub.Client, enc, topic,
		protocol.WithTopicTokens{"token": "test"},
	)
	require.NoError(t, err)

	done, err := protocol.Listen(ctx, receiver)
	require.NoError(t, err)
	defer done()

	err = sender.Send(ctx, value)
	require.NoError(t, err)

	res := <-results
	require.Equal(t, stub.Client.ClientID(), res.ClientID)
	require.Equal(t, value, res.Payload)
}
