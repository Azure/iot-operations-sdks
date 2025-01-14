// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/stretchr/testify/require"
)

// Simple happy-path sanity check.
func TestCommand(t *testing.T) {
	ctx := context.Background()
	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	enc := protocol.JSON[string]{}
	topic := "prefix/{ex:token}/suffix"
	value := "test"

	executor, err := protocol.NewCommandExecutor(server, enc, enc, topic,
		func(
			_ context.Context,
			cr *protocol.CommandRequest[string],
		) (*protocol.CommandResponse[string], error) {
			require.Equal(t, "application/json", cr.ContentType)
			require.Equal(t, byte(1), cr.PayloadFormat)
			return protocol.Respond(
				cr.Payload+cr.ClientID+cr.CorrelationData,
				protocol.WithMetadata(cr.TopicTokens),
			)
		},
		protocol.WithTopicNamespace("ns"),
	)
	require.NoError(t, err)
	listeners = append(listeners, executor)

	invoker, err := protocol.NewCommandInvoker(client, enc, enc, topic,
		protocol.WithResponseTopicSuffix("response/{executorId}"),
		protocol.WithTopicNamespace("ns"),
		protocol.WithTopicTokens{"token": "test"},
		protocol.WithTopicTokenNamespace("ex:"),
	)
	require.NoError(t, err)
	listeners = append(listeners, invoker)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	res, err := invoker.Invoke(ctx, value,
		protocol.WithTopicTokens{"executorId": server.ID()},
	)
	require.NoError(t, err)

	expected := value + client.ID() + res.CorrelationData
	require.Equal(t, expected, res.Payload)
	require.Equal(t, map[string]string{"ex:token": "test"}, res.Metadata)
	require.Equal(t, map[string]string{
		"ex:token":   "test",
		"executorId": server.ID(),
	}, res.TopicTokens)
}

func TestCommandError(t *testing.T) {
	ctx := context.Background()
	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	req := protocol.Empty{}
	res := protocol.JSON[string]{}
	topic := "topic"

	executor, err := protocol.NewCommandExecutor(server, req, res, topic,
		func(
			context.Context,
			*protocol.CommandRequest[any],
		) (*protocol.CommandResponse[string], error) {
			return nil, protocol.InvocationError{Message: "user error"}
		},
	)
	require.NoError(t, err)
	listeners = append(listeners, executor)

	invoker, err := protocol.NewCommandInvoker(client, req, res, topic,
		protocol.WithResponseTopicSuffix("response"),
	)
	require.NoError(t, err)
	listeners = append(listeners, invoker)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	_, err = invoker.Invoke(ctx, nil)
	require.Error(t, err)
	require.Equal(t, err.Error(), "user error")
}
