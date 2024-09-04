/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type ResetCommandInvoker struct {
	*protocol.CommandInvoker[any, any]
}

func NewResetCommandInvoker(
	client mqtt.Client,
	requestTopic string,
	opt ...protocol.CommandInvokerOption,
) (*ResetCommandInvoker, error) {
	var err error
	invoker := &ResetCommandInvoker{}

	var opts protocol.CommandInvokerOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokenNamespace("ex:"),
		protocol.WithTopicTokens{
			"commandName":     "reset",
			"invokerClientId": client.ClientID(),
		},
	)

	invoker.CommandInvoker, err = protocol.NewCommandInvoker(
		client,
		protocol.Empty{},
		protocol.Empty{},
		requestTopic,
		&opts,
	)

	return invoker, err
}

func (invoker ResetCommandInvoker) Reset(
	ctx context.Context,
	executorId string,
	opt ...protocol.InvokeOption,
) error {
	var opts protocol.InvokeOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"executorId": executorId,
		},
	)

	_, err := invoker.Invoke(
		ctx,
		nil,
		&opts,
	)

	return err
}
