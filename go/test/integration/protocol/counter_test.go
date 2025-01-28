// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"sync/atomic"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	counter "github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/stretchr/testify/require"
)

type Handlers struct{ counter int32 }

func (h *Handlers) ReadCounter(
	context.Context,
	*protocol.CommandRequest[any],
) (*protocol.CommandResponse[counter.ReadCounterResponsePayload], error) {
	response := counter.ReadCounterResponsePayload{
		CounterResponse: atomic.LoadInt32(&h.counter),
	}
	return protocol.Respond(response)
}

func (h *Handlers) Increment(
	_ context.Context,
	req *protocol.CommandRequest[counter.IncrementRequestPayload],
) (*protocol.CommandResponse[counter.IncrementResponsePayload], error) {
	response := counter.IncrementResponsePayload{
		CounterResponse: atomic.AddInt32(
			&h.counter,
			req.Payload.IncrementValue,
		),
	}
	return protocol.Respond(response)
}

func (h *Handlers) Reset(
	context.Context,
	*protocol.CommandRequest[any],
) (*protocol.CommandResponse[any], error) {
	atomic.StoreInt32(&h.counter, 0)
	return protocol.Respond[any](nil)
}

func TelemetryNoOp(
	context.Context,
	*protocol.TelemetryMessage[counter.TelemetryCollection],
) error {
	return nil
}

func TestIncrement(t *testing.T) {
	ctx := context.Background()
	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	counterService, err := counter.NewCounterService(app, server, &Handlers{})
	require.NoError(t, err)
	listeners = append(listeners, counterService)

	counterClient, err := counter.NewCounterClient(app, client, TelemetryNoOp)
	require.NoError(t, err)
	listeners = append(listeners, counterClient)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	executorID := server.ID()
	one := counter.IncrementRequestPayload{IncrementValue: 1}

	readRes, err := counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(0), readRes.Payload.CounterResponse)

	incrRes, err := counterClient.Increment(ctx, executorID, one)
	require.NoError(t, err)
	require.Equal(t, int32(1), incrRes.Payload.CounterResponse)

	readRes, err = counterClient.ReadCounter(ctx, executorID)
	require.NoError(t, err)
	require.Equal(t, int32(1), readRes.Payload.CounterResponse)

	for i := int32(2); i <= 4; i++ {
		incrRes, err := counterClient.Increment(ctx, executorID, one)
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
