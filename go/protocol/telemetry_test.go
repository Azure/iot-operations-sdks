// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol_test

import (
	"context"
	"net/url"
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
	defer receiver.Close()

	sender, err := protocol.NewTelemetrySender(stub.Client, enc, topic,
		protocol.WithTopicTokens{"token": "test"},
	)
	require.NoError(t, err)

	err = receiver.Start(ctx)
	require.NoError(t, err)

	source, err := url.Parse("https://contoso.com")
	require.NoError(t, err)

	err = sender.Send(ctx, value, &protocol.CloudEvent{Source: source})
	require.NoError(t, err)

	res := <-results
	require.Equal(t, stub.Client.ID(), res.ClientID)
	require.Equal(t, value, res.Payload)
	require.Equal(t, "https://contoso.com", res.Source.String())
	require.Equal(t, "prefix/test/suffix", res.Subject)
	require.Equal(t, enc.ContentType(), res.DataContentType)
}
