// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/iso"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/greeter/envoy"
	"github.com/stretchr/testify/require"
)

func TestSayHello(t *testing.T) {
	ctx := context.Background()

	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	encReq := protocol.JSON[envoy.HelloRequest]{}
	encRes := protocol.JSON[envoy.HelloResponse]{}
	topic := "prefix/{ex:token}/suffix"

	executor, err := protocol.NewCommandExecutor(server, encReq, encRes, topic,
		func(
			_ context.Context,
			cr *protocol.CommandRequest[envoy.HelloRequest],
		) (*protocol.CommandResponse[envoy.HelloResponse], error) {
			fmt.Printf(
				"--> Executing Greeter.SayHello with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			response := envoy.HelloResponse{
				Message: "Hello " + cr.Payload.Name,
			}
			fmt.Printf(
				"--> Executed Greeter.SayHello with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			return protocol.Respond(
				response,
				protocol.WithMetadata(cr.TopicTokens),
			)
		},
		protocol.WithTopicNamespace("ns"),
	)
	require.NoError(t, err)
	listeners = append(listeners, executor)

	invoker, err := protocol.NewCommandInvoker(client, encReq, encRes, topic,
		protocol.WithResponseTopicSuffix("response/{executorId}"),
		protocol.WithTopicNamespace("ns"),
		protocol.WithTopicTokens{"token": "test"},
		protocol.WithTopicTokenNamespace("ex:"),
	)
	require.NoError(t, err)
	listeners = append(listeners, invoker)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	req := envoy.HelloRequest{Name: "User"}
	res, err := invoker.Invoke(ctx, req,
		protocol.WithTopicTokens{"executorId": server.ID()},
	)
	require.NoError(t, err)

	expected := "Hello " + req.Name
	require.Equal(t, expected, res.Payload.Message)
}

func TestSayHelloWithDelay(t *testing.T) {
	ctx := context.Background()
	client, server, done := sessionClients(t)
	defer done()
	var listeners protocol.Listeners
	defer listeners.Close()
	encReq := protocol.JSON[envoy.HelloWithDelayRequest]{}
	encRes := protocol.JSON[envoy.HelloResponse]{}
	topic := "prefix/{ex:token}/suffix"
	executor, err := protocol.NewCommandExecutor(server, encReq, encRes, topic,
		func(
			_ context.Context,
			cr *protocol.CommandRequest[envoy.HelloWithDelayRequest],
		) (*protocol.CommandResponse[envoy.HelloResponse], error) {
			fmt.Printf(
				"--> Executing Greeter.SayHelloWithDelay with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			response := envoy.HelloResponse{
				Message: fmt.Sprintf(
					"Hello %s after %s",
					cr.Payload.HelloRequest.Name,
					cr.Payload.Delay,
				),
			}
			fmt.Printf(
				"--> Executed Greeter.SayHelloWithDelay with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			return protocol.Respond(
				response,
				protocol.WithMetadata(cr.TopicTokens),
			)
		},
		protocol.WithTopicNamespace("ns"),
	)
	require.NoError(t, err)
	listeners = append(listeners, executor)

	invoker, err := protocol.NewCommandInvoker(client, encReq, encRes, topic,
		protocol.WithResponseTopicSuffix("response/{executorId}"),
		protocol.WithTopicNamespace("ns"),
		protocol.WithTopicTokens{"token": "test"},
		protocol.WithTopicTokenNamespace("ex:"),
	)
	require.NoError(t, err)
	listeners = append(listeners, invoker)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	req := envoy.HelloWithDelayRequest{
		HelloRequest: envoy.HelloRequest{Name: "User"},
		Delay:        iso.Duration(time.Second * 2),
	}
	res, err := invoker.Invoke(ctx, req,
		protocol.WithTopicTokens{"executorId": server.ID()},
	)
	require.NoError(t, err)

	expected := fmt.Sprintf(
		"Hello %s after %s",
		req.HelloRequest.Name,
		req.Delay,
	)
	require.Equal(t, expected, res.Payload.Message)
}

func TestSayHelloWithDelayZeroThrows(t *testing.T) {
	ctx := context.Background()

	client, server, done := sessionClients(t)
	defer done()

	var listeners protocol.Listeners
	defer listeners.Close()

	encReq := protocol.JSON[envoy.HelloWithDelayRequest]{}
	encRes := protocol.JSON[envoy.HelloResponse]{}
	topic := "prefix/{ex:token}/suffix"

	executor, err := protocol.NewCommandExecutor(
		server,
		encReq,
		encRes,
		topic,
		func(_ context.Context, cr *protocol.CommandRequest[envoy.HelloWithDelayRequest]) (*protocol.CommandResponse[envoy.HelloResponse], error) {
			fmt.Printf(
				"--> Executing Greeter.SayHelloWithDelay with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			if cr.Payload.Delay == 0 {
				return nil, fmt.Errorf("Delay cannot be Zero")
			}
			response := envoy.HelloResponse{
				Message: fmt.Sprintf(
					"Hello %s after %s",
					cr.Payload.HelloRequest.Name,
					cr.Payload.Delay,
				),
			}
			fmt.Printf(
				"--> Executed Greeter.SayHelloWithDelay with id %s for %s\n",
				cr.CorrelationData,
				cr.ClientID,
			)
			return protocol.Respond(
				response,
				protocol.WithMetadata(cr.TopicTokens),
			)
		},
		protocol.WithTopicNamespace("ns"),
	)
	require.NoError(t, err)
	listeners = append(listeners, executor)

	invoker, err := protocol.NewCommandInvoker(client, encReq, encRes, topic,
		protocol.WithResponseTopicSuffix("response/{executorId}"),
		protocol.WithTopicNamespace("ns"),
		protocol.WithTopicTokens{"token": "test"},
		protocol.WithTopicTokenNamespace("ex:"),
	)
	require.NoError(t, err)
	listeners = append(listeners, invoker)

	err = listeners.Start(ctx)
	require.NoError(t, err)

	req := envoy.HelloWithDelayRequest{
		HelloRequest: envoy.HelloRequest{Name: "User"},
		Delay:        iso.Duration(0),
	}
	_, err = invoker.Invoke(
		ctx,
		req,
		protocol.WithTopicTokens{"executorId": server.ID()},
	)
	require.Error(t, err)
	require.Contains(t, err.Error(), "Delay cannot be Zero")
}
