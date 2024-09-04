/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type ReadCounterCommandInvoker struct {
	*protocol.CommandInvoker[any, ReadCounterCommandResponse]
}

func NewReadCounterCommandInvoker(
	client mqtt.Client,
	requestTopic string,
	opt ...protocol.CommandInvokerOption,
) (*ReadCounterCommandInvoker, error) {
	var err error
	invoker := &ReadCounterCommandInvoker{}

	var opts protocol.CommandInvokerOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokenNamespace("ex:"),
		protocol.WithTopicTokens{
			"commandName":     "readCounter",
			"invokerClientId": client.ClientID(),
		},
	)

	invoker.CommandInvoker, err = protocol.NewCommandInvoker(
		client,
		protocol.Empty{},
		protocol.JSON[ReadCounterCommandResponse]{},
		requestTopic,
		&opts,
	)

	return invoker, err
}

func (invoker ReadCounterCommandInvoker) ReadCounter(
	ctx context.Context,
	executorId string,
	opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[ReadCounterCommandResponse], error) {
	var opts protocol.InvokeOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"executorId": executorId,
		},
	)

	response, err := invoker.Invoke(
		ctx,
		nil,
		&opts,
	)

	return response, err
}
