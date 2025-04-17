// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT.
package adrbaseservice

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type UpdateAssetStatusCommandInvoker struct {
	*protocol.CommandInvoker[UpdateAssetStatusRequestPayload, UpdateAssetStatusResponsePayload]
}

func NewUpdateAssetStatusCommandInvoker(
	app *protocol.Application,
	client protocol.MqttClient,
	requestTopic string,
	opt ...protocol.CommandInvokerOption,
) (*UpdateAssetStatusCommandInvoker, error) {
	var err error
	invoker := &UpdateAssetStatusCommandInvoker{}

	var opts protocol.CommandInvokerOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"commandName": "updateAssetStatus",
		},
	)

	invoker.CommandInvoker, err = protocol.NewCommandInvoker(
		app,
		client,
		protocol.JSON[UpdateAssetStatusRequestPayload]{},
		protocol.JSON[UpdateAssetStatusResponsePayload]{},
		requestTopic,
		&opts,
	)

	return invoker, err
}

func (invoker UpdateAssetStatusCommandInvoker) UpdateAssetStatus(
	ctx context.Context,
	request UpdateAssetStatusRequestPayload,
	opt ...protocol.InvokeOption,
) (*protocol.CommandResponse[UpdateAssetStatusResponsePayload], error) {
	invokerOpts := []protocol.InvokeOption{
		protocol.WithTopicTokenNamespace("ex:"),
	}

	var invokeOpts protocol.InvokeOptions
	invokeOpts.Apply(opt, invokerOpts...)

	response, err := invoker.Invoke(
		ctx,
		request,
		&invokeOpts,
	)

	return response, err
}
