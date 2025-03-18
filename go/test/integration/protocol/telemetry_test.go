// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

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
	client, server, done := sessionClients(t)
	defer done()

	enc := protocol.Custom{}
	topic := "prefix/{token}/suffix"
	value := protocol.Data{
		Payload:     []byte("value"),
		ContentType: "custom/type",
	}

	results := make(chan *protocol.TelemetryMessage[protocol.Data])

	receiver, err := protocol.NewTelemetryReceiver(
		app, server, enc, topic,
		func(
			_ context.Context,
			tm *protocol.TelemetryMessage[protocol.Data],
		) error {
			results <- tm
			return nil
		},
	)
	require.NoError(t, err)
	defer receiver.Close()

	sender, err := protocol.NewTelemetrySender(
		app, client, enc, topic,
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
	require.Equal(t, client.ID(), res.ClientID)
	require.Equal(t, value, res.Payload)

	cloudEvent, err := protocol.CloudEventFromTelemetry(res)
	require.NoError(t, err)
	require.Equal(t, "https://contoso.com", cloudEvent.Source.String())
	require.Equal(t, "prefix/test/suffix", cloudEvent.Subject)
	require.Equal(t, value.ContentType, cloudEvent.DataContentType)
}
