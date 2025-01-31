// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package envoy

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/iso"
)

type (
	HelloRequest struct {
		Name string
	}

	HelloWithDelayRequest struct {
		HelloRequest
		Delay iso.Duration
	}

	HelloResponse struct {
		Message string
	}

	GreeterHandlers interface {
		SayHello(
			context.Context,
			*protocol.CommandRequest[HelloRequest],
		) (*protocol.CommandResponse[HelloResponse], error)

		SayHelloWithDelay(
			context.Context,
			*protocol.CommandRequest[HelloWithDelayRequest],
		) (*protocol.CommandResponse[HelloResponse], error)
	}

	GreeterServer struct {
		protocol.Listeners
		sayHelloExecutor *protocol.CommandExecutor[
			HelloRequest,
			HelloResponse,
		]
		sayHelloWithDelayExecutor *protocol.CommandExecutor[
			HelloWithDelayRequest,
			HelloResponse,
		]
	}

	GreeterClient struct {
		protocol.Listeners
		sayHelloInvoker *protocol.CommandInvoker[
			HelloRequest,
			HelloResponse,
		]
		sayHelloWithDelayInvoker *protocol.CommandInvoker[
			HelloWithDelayRequest,
			HelloResponse,
		]
	}
)

const (
	SayHelloCommandTopic          = "rpc/samples/hello"
	SayHelloWithDelayCommandTopic = "rpc/samples/hello/delay"
)

var (
	HelloRequestEncoding          = protocol.JSON[HelloRequest]{}
	HelloWithDelayRequestEncoding = protocol.JSON[HelloWithDelayRequest]{}
	HelloResponseEncoding         = protocol.JSON[HelloResponse]{}
)

func NewGreeterServer(
	app *protocol.Application,
	client protocol.MqttClient,
	handlers GreeterHandlers,
	opts ...protocol.CommandExecutorOption,
) (*GreeterServer, error) {
	s := &GreeterServer{}
	var err error

	var opt protocol.CommandExecutorOptions
	opt.Apply(opts, protocol.WithTopicTokens{
		"executorId": client.ID(),
	})

	s.sayHelloExecutor, err = protocol.NewCommandExecutor(
		app,
		client,
		HelloRequestEncoding,
		HelloResponseEncoding,
		SayHelloCommandTopic,
		handlers.SayHello,
		&opt,
	)
	if err != nil {
		s.Close()
		return nil, err
	}
	s.Listeners = append(s.Listeners, s.sayHelloExecutor)

	s.sayHelloWithDelayExecutor, err = protocol.NewCommandExecutor(
		app,
		client,
		HelloWithDelayRequestEncoding,
		HelloResponseEncoding,
		SayHelloWithDelayCommandTopic,
		handlers.SayHelloWithDelay,
		&opt,
		protocol.WithIdempotent(true),
		protocol.WithTimeout(30*time.Second),
	)
	if err != nil {
		s.Close()
		return nil, err
	}
	s.Listeners = append(s.Listeners, s.sayHelloWithDelayExecutor)

	return s, nil
}

func NewGreeterClient(
	app *protocol.Application,
	client protocol.MqttClient,
	opts ...protocol.CommandInvokerOption,
) (*GreeterClient, error) {
	c := &GreeterClient{}
	var err error

	var opt protocol.CommandInvokerOptions
	opt.Apply(opts, protocol.WithTopicTokens{
		"invokerClientId": client.ID(),
	})

	if opt.ResponseTopicPrefix == "" && opt.ResponseTopicSuffix == "" {
		opt.ResponseTopicPrefix = "clients/{invokerClientId}"
	}

	c.sayHelloInvoker, err = protocol.NewCommandInvoker(
		app,
		client,
		HelloRequestEncoding,
		HelloResponseEncoding,
		SayHelloCommandTopic,
		&opt,
	)
	if err != nil {
		c.Close()
		return nil, err
	}
	c.Listeners = append(c.Listeners, c.sayHelloInvoker)

	c.sayHelloWithDelayInvoker, err = protocol.NewCommandInvoker(
		app,
		client,
		HelloWithDelayRequestEncoding,
		HelloResponseEncoding,
		SayHelloWithDelayCommandTopic,
		&opt,
	)
	if err != nil {
		c.Close()
		return nil, err
	}
	c.Listeners = append(c.Listeners, c.sayHelloInvoker)

	return c, nil
}

func (c *GreeterClient) SayHello(
	ctx context.Context,
	req HelloRequest,
	opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[HelloResponse], error) {
	return c.sayHelloInvoker.Invoke(ctx, req, opt...)
}

func (c *GreeterClient) SayHelloWithDelay(
	ctx context.Context,
	req HelloWithDelayRequest,
	opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[HelloResponse], error) {
	return c.sayHelloWithDelayInvoker.Invoke(ctx, req, opt...)
}
