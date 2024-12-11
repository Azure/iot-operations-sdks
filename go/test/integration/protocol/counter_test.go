// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/stretchr/testify/require"
)

func TestIncrement(t *testing.T) {
	ctx := context.Background()
	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	ResetCounter()

	counterService, err := dtmi_com_example_Counter__1.NewCounterService(
		server,
		func(
			_ context.Context,
			_ *protocol.CommandRequest[any],
		) (*protocol.CommandResponse[dtmi_com_example_Counter__1.ReadCounterResponsePayload], error) {
			response := dtmi_com_example_Counter__1.ReadCounterResponsePayload{
				CounterResponse: ReadCounter(),
			}
			resp, err := protocol.Respond[dtmi_com_example_Counter__1.ReadCounterResponsePayload](
				response,
			)
			if err != nil {
				return nil, err
			}
			return resp, nil
		},
		func(
			_ context.Context,
			_ *protocol.CommandRequest[any],
		) (*protocol.CommandResponse[dtmi_com_example_Counter__1.IncrementResponsePayload], error) {
			newValue := IncrementCounter()
			response := dtmi_com_example_Counter__1.IncrementResponsePayload{
				CounterResponse: newValue,
			}
			resp, err := protocol.Respond[dtmi_com_example_Counter__1.IncrementResponsePayload](
				response,
			)
			if err != nil {
				return nil, err
			}
			return resp, nil
		},
		func(
			_ context.Context,
			_ *protocol.CommandRequest[any],
		) (*protocol.CommandResponse[any], error) {
			ResetCounter()
			resp, err := protocol.Respond[any](nil)
			if err != nil {
				return nil, err
			}
			return resp, nil
		},
	)
	require.NoError(t, err)
	listeners = append(listeners, counterService)

	counterClient, err := dtmi_com_example_Counter__1.NewCounterClient(client)
	require.NoError(t, err)
	listeners = append(listeners, counterClient)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	executorID := server.ID()

	readRes, err := counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(0), readRes.Payload.CounterResponse)

	incrRes, err := counterClient.Increment(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(1), incrRes.Payload.CounterResponse)

	readRes, err = counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(1), readRes.Payload.CounterResponse)

	for i := int32(2); i <= 4; i++ {
		incrRes, err := counterClient.Increment(ctx, executorID)
		require.NoError(t, err)
		require.Equal(t, i, incrRes.Payload.CounterResponse)
	}

	readRes, err = counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(4), readRes.Payload.CounterResponse)

	err = counterClient.Reset(ctx, executorID)
	require.NoError(t, err)

	readRes, err = counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(0), readRes.Payload.CounterResponse)
}
