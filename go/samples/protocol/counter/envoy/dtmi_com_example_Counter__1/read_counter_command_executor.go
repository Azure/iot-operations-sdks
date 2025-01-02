// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT.
package dtmi_com_example_Counter__1

import (
	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type ReadCounterCommandExecutor struct {
	*protocol.CommandExecutor[any, ReadCounterResponsePayload]
}

func NewReadCounterCommandExecutor(
	client protocol.MqttClient,
	requestTopic string,
	handler protocol.CommandHandler[any, ReadCounterResponsePayload],
	opt ...protocol.CommandExecutorOption,
) (*ReadCounterCommandExecutor, error) {
	var err error
	executor := &ReadCounterCommandExecutor{}

	var opts protocol.CommandExecutorOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"commandName": "readCounter",
		},
		protocol.WithIdempotent(false),
	)

	executor.CommandExecutor, err = protocol.NewCommandExecutor(
		client,
		protocol.Empty{},
		protocol.JSON[ReadCounterResponsePayload]{},
		requestTopic,
		handler,
		&opts,
	)

	return executor, err
}
