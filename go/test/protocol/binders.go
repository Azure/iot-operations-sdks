// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

type (
	TestingCommandInvoker struct {
		base *protocol.CommandInvoker[string, string]
	}
)

type (
	TestingCommandExecutor struct {
		base           *protocol.CommandExecutor[string, string]
		executionCount int
		reqRespSeq     sync.Map
	}
)

type (
	TestingTelemetrySender struct {
		base *protocol.TelemetrySender[string]
	}
)

type (
	TestingTelemetryReceiver struct {
		base           *protocol.TelemetryReceiver[string]
		telemetryCount int
	}
)

var app = protocol.Must(protocol.NewApplication())

func NewTestingCommandInvoker(
	client protocol.MqttClient,
	commandName *string,
	serializer *TestCaseSerializer,
	requestTopic *string,
	opt ...protocol.CommandInvokerOption,
) (*TestingCommandInvoker, error) {
	invoker := &TestingCommandInvoker{}
	var err error

	if commandName == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "commandName is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "commandName",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	if requestTopic == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "requestTopic is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "requesttopicpattern",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	var opts protocol.CommandInvokerOptions
	opts.Apply(
		opt,
	)

	invoker.base, err = protocol.NewCommandInvoker(
		app,
		client,
		serializer,
		serializer,
		*requestTopic,
		&opts,
	)

	return invoker, err
}

func NewTestingCommandExecutor(
	client protocol.MqttClient,
	commandName *string,
	serializer *TestCaseSerializer,
	requestTopic *string,
	handler func(context.Context, *protocol.CommandRequest[string], *sync.Map) (*protocol.CommandResponse[string], error),
	opt ...protocol.CommandExecutorOption,
) (*TestingCommandExecutor, error) {
	executor := &TestingCommandExecutor{
		executionCount: 0,
	}
	var err error

	if commandName == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "commandName is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "commandName",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	if requestTopic == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "requestTopic is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "requesttopicpattern",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	var opts protocol.CommandExecutorOptions
	opts.Apply(
		opt,
	)

	executor.base, err = protocol.NewCommandExecutor(
		app,
		client,
		serializer,
		serializer,
		*requestTopic,
		func(
			ctx context.Context,
			req *protocol.CommandRequest[string],
		) (*protocol.CommandResponse[string], error) {
			executor.executionCount++
			return handler(ctx, req, &executor.reqRespSeq)
		},
		&opts,
	)

	return executor, err
}

func NewTestingTelemetrySender(
	client protocol.MqttClient,
	serializer *TestCaseSerializer,
	telemetryTopic *string,
	opt ...protocol.TelemetrySenderOption,
) (*TestingTelemetrySender, error) {
	sender := &TestingTelemetrySender{}
	var err error

	if telemetryTopic == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "telemetryTopic is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "topicpattern",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	var opts protocol.TelemetrySenderOptions
	opts.Apply(
		opt,
	)

	sender.base, err = protocol.NewTelemetrySender(
		app,
		client,
		serializer,
		*telemetryTopic,
		&opts,
	)

	return sender, err
}

func NewTestingTelemetryReceiver(
	client protocol.MqttClient,
	serializer *TestCaseSerializer,
	telemetryTopic *string,
	handler func(context.Context, *protocol.TelemetryMessage[string]) error,
	opt ...protocol.TelemetryReceiverOption,
) (*TestingTelemetryReceiver, error) {
	receiver := &TestingTelemetryReceiver{
		telemetryCount: 0,
	}
	var err error

	if telemetryTopic == nil {
		return nil, &errors.Client{
			Base: errors.Base{
				Message:       "telemetryTopic is nil",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "topicpattern",
				PropertyValue: nil,
			},
			IsShallow: true,
		}
	}

	var opts protocol.TelemetryReceiverOptions
	opts.Apply(
		opt,
	)

	receiver.base, err = protocol.NewTelemetryReceiver(
		app,
		client,
		serializer,
		*telemetryTopic,
		func(
			ctx context.Context,
			msg *protocol.TelemetryMessage[string],
		) error {
			receiver.telemetryCount++
			return handler(ctx, msg)
		},
		&opts,
	)

	return receiver, err
}
