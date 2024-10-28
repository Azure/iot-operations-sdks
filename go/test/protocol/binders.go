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

func NewTestingCommandInvoker(
	client protocol.MqttClient,
	commandName *string,
	requestTopic *string,
	modelID *string,
	opt ...protocol.CommandInvokerOption,
) (*TestingCommandInvoker, error) {
	invoker := &TestingCommandInvoker{}
	var err error

	if commandName == nil {
		return nil, &errors.Error{
			Message:       "commandName is nil",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "commandName",
			PropertyValue: nil,
			IsShallow:     true,
		}
	}

	if requestTopic == nil {
		return nil, &errors.Error{
			Message:       "requestTopic is nil",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "requesttopicpattern",
			PropertyValue: nil,
			IsShallow:     true,
		}
	}

	var opts protocol.CommandInvokerOptions
	if modelID != nil {
		opts.Apply(
			opt,
			protocol.WithTopicTokens{
				"modelId":         *modelID,
				"invokerClientId": client.ID(),
			},
		)
	} else {
		opts.Apply(
			opt,
			protocol.WithTopicTokens{
				"invokerClientId": client.ID(),
			},
		)
	}

	invoker.base, err = protocol.NewCommandInvoker(
		client,
		protocol.JSON[string]{},
		protocol.JSON[string]{},
		*requestTopic,
		&opts,
		protocol.WithTopicTokens{"commandName": *commandName},
	)

	return invoker, err
}

func NewTestingCommandExecutor(
	client protocol.MqttClient,
	commandName *string,
	requestTopic *string,
	handler func(context.Context, *protocol.CommandRequest[string], *sync.Map) (*protocol.CommandResponse[string], error),
	modelID *string,
	executorID *string,
	opt ...protocol.CommandExecutorOption,
) (*TestingCommandExecutor, error) {
	executor := &TestingCommandExecutor{
		executionCount: 0,
	}
	var err error

	if commandName == nil {
		return nil, &errors.Error{
			Message:       "commandName is nil",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "commandName",
			PropertyValue: nil,
			IsShallow:     true,
		}
	}

	if requestTopic == nil {
		return nil, &errors.Error{
			Message:       "requestTopic is nil",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "requesttopicpattern",
			PropertyValue: nil,
			IsShallow:     true,
		}
	}

	var opts protocol.CommandExecutorOptions
	if modelID != nil {
		opts.Apply(
			opt,
			protocol.WithTopicTokens{
				"modelId": *modelID,
			},
		)
	}

	if executorID != nil {
		opts.Apply(
			opt,
			protocol.WithTopicTokens{
				"executorId": *executorID,
			},
		)
	} else {
		opts.Apply(
			opt,
			protocol.WithTopicTokens{
				"executorId": client.ID(),
			},
		)
	}

	executor.base, err = protocol.NewCommandExecutor(
		client,
		protocol.JSON[string]{},
		protocol.JSON[string]{},
		*requestTopic,
		func(
			ctx context.Context,
			req *protocol.CommandRequest[string],
		) (*protocol.CommandResponse[string], error) {
			executor.executionCount++
			return handler(ctx, req, &executor.reqRespSeq)
		},
		&opts,
		protocol.WithTopicTokens{"commandName": *commandName},
	)

	return executor, err
}
