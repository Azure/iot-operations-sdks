package protocol_test

import (
	"context"
	"testing"

	"github.com/microsoft/mqtt-patterns/lib/go/protocol"
	"github.com/stretchr/testify/require"
)

// Simple happy-path sanity check.
func TestCommand(t *testing.T) {
	ctx := context.Background()
	stub := setupMqtt(ctx, t, 1885)
	defer stub.Broker.Close()

	enc := protocol.JSON[string]{}
	topic := "prefix/{ex:token}/suffix"
	value := "test"

	executor, err := protocol.NewCommandExecutor(stub.Server, enc, enc, topic,
		func(
			_ context.Context,
			cr *protocol.CommandRequest[string],
		) (*protocol.CommandResponse[string], error) {
			return protocol.Respond(
				cr.Payload+cr.ClientID+cr.CorrelationData,
				protocol.WithMetadata{"key": "value"},
			)
		},
		protocol.WithTopicNamespace("ns"),
	)
	require.NoError(t, err)

	invoker, err := protocol.NewCommandInvoker(stub.Client, enc, enc, topic,
		protocol.WithResponseTopicSuffix("response/{executorId}"),
		protocol.WithTopicNamespace("ns"),
		protocol.WithTopicTokens{"token": "test"},
		protocol.WithTopicTokenNamespace("ex:"),
	)
	require.NoError(t, err)

	done, err := protocol.Listen(ctx, executor, invoker)
	require.NoError(t, err)
	defer done()

	res, err := invoker.Invoke(ctx, value,
		protocol.WithTopicTokens{"executorId": stub.Server.ClientID()},
	)
	require.NoError(t, err)

	expected := value + stub.Client.ClientID() + res.CorrelationData
	require.Equal(t, expected, res.Payload)
	require.Equal(t, map[string]string{"key": "value"}, res.Metadata)
}

func TestCommandError(t *testing.T) {
	ctx := context.Background()
	stub := setupMqtt(ctx, t, 1885)
	defer stub.Broker.Close()

	req := protocol.Empty{}
	res := protocol.JSON[string]{}
	topic := "topic"

	executor, err := protocol.NewCommandExecutor(stub.Server, req, res, topic,
		func(
			context.Context,
			*protocol.CommandRequest[any],
		) (*protocol.CommandResponse[string], error) {
			return nil, protocol.InvocationError{Message: "user error"}
		},
	)
	require.NoError(t, err)

	invoker, err := protocol.NewCommandInvoker(stub.Client, req, res, topic,
		protocol.WithResponseTopicSuffix("response"),
	)
	require.NoError(t, err)

	done, err := protocol.Listen(ctx, executor, invoker)
	require.NoError(t, err)
	defer done()

	_, err = invoker.Invoke(ctx, nil)
	require.Error(t, err)
	require.Equal(t, err.Error(), "user error")
}
