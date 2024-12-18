// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"fmt"
	"sync/atomic"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/greeter/envoy"
	"github.com/stretchr/testify/require"
)

func sessionClients(
	t *testing.T,
) (client, server *mqtt.SessionClient, done func()) {
	conn := mqtt.TCPConnection("localhost", 1883)

	client = mqtt.NewSessionClient(conn)
	require.NoError(t, client.Start())

	server = mqtt.NewSessionClient(conn)
	require.NoError(t, server.Start())

	return client, server, func() {
		require.NoError(t, client.Stop())
		require.NoError(t, server.Stop())
	}
}

type GreeterService struct {
	client protocol.MqttClient
}

func NewGreeterService(client protocol.MqttClient) *GreeterService {
	return &GreeterService{client: client}
}

func SayHello(
	ctx context.Context,
	req *protocol.CommandRequest[envoy.HelloRequest],
) (*protocol.CommandResponse[envoy.HelloResponse], error) {
	select {
	case <-ctx.Done():
		return nil, ctx.Err()
	default:
	}

	fmt.Printf(
		"--> Executing Greeter.SayHello with id %s for %s\n",
		req.CorrelationData,
		req.ClientID,
	)

	fmt.Printf(
		"--> Executed Greeter.SayHello with id %s for %s\n",
		req.CorrelationData,
		req.ClientID,
	)

	return protocol.Respond(
		envoy.HelloResponse{
			Message: "Hello " + req.Payload.Name,
		},
		protocol.WithMetadata(req.TopicTokens),
	)
}

func SayHelloWithDelay(
	ctx context.Context,
	req *protocol.CommandRequest[envoy.HelloWithDelayRequest],
) (*protocol.CommandResponse[envoy.HelloResponse], error) {
	fmt.Printf(
		"--> Executing Greeter.SayHelloWithDelay with id %s for %s\n",
		req.CorrelationData,
		req.ClientID,
	)

	if req.Payload.Delay == 0 {
		return nil, fmt.Errorf("Delay cannot be Zero")
	}

	select {
	case <-time.After(time.Duration(req.Payload.Delay)):
	case <-ctx.Done():
		return nil, ctx.Err()
	}

	fmt.Printf(
		"--> Executed Greeter.SayHelloWithDelay with id %s for %s\n",
		req.CorrelationData,
		req.ClientID,
	)

	return protocol.Respond(
		envoy.HelloResponse{
			Message: fmt.Sprintf(
				"Hello %s after %s",
				req.Payload.HelloRequest.Name,
				req.Payload.Delay,
			),
		},
		protocol.WithMetadata(req.TopicTokens),
	)
}

var counter int32

func IncrementCounter() int32 {
	return atomic.AddInt32(&counter, 1)
}

func ReadCounter() int32 {
	return atomic.LoadInt32(&counter)
}

func ResetCounter() {
	atomic.StoreInt32(&counter, 0)
}
