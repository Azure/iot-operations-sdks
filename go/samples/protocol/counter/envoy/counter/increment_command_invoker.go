// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT.
package counter

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type IncrementCommandInvoker struct {
	*protocol.CommandInvoker[IncrementRequestPayload, IncrementResponsePayload]
}

func NewIncrementCommandInvoker(
	app *protocol.Application,
	client protocol.MqttClient,
	requestTopic string,
	opt ...protocol.CommandInvokerOption,
) (*IncrementCommandInvoker, error) {
	var err error
	invoker := &IncrementCommandInvoker{}

	var opts protocol.CommandInvokerOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"commandName":     "increment",
		},
	)

	invoker.CommandInvoker, err = protocol.NewCommandInvoker(
		app,
		client,
		protocol.JSON[IncrementRequestPayload]{},
		protocol.JSON[IncrementResponsePayload]{},
		requestTopic,
		&opts,
	)

	return invoker, err
}

func (invoker IncrementCommandInvoker) Increment(
	ctx context.Context,
	executorId string,
	request IncrementRequestPayload,
	opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[IncrementResponsePayload], error) {
	var opts protocol.InvokeOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"executorId": executorId,
		},
	)

	response, err := invoker.Invoke(
		ctx,
		request,
		&opts,
	)

	return response, err
}
