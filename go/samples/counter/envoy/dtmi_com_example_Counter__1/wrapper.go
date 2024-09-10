/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type CounterService struct {
	*ReadCounterCommandExecutor
	*IncrementCommandExecutor
	*ResetCommandExecutor
}

type CounterClient struct {
	*ReadCounterCommandInvoker
	*IncrementCommandInvoker
	*ResetCommandInvoker
}

const (
	CommandTopic = "rpc/command-samples/{executorId}/{commandName}"
)

func NewCounterService(
	client mqtt.Client,
	readCounterHandler protocol.CommandHandler[any, ReadCounterCommandResponse],
	incrementHandler protocol.CommandHandler[any, IncrementCommandResponse],
	resetHandler protocol.CommandHandler[any, any],
	opts ...protocol.Option,
) (*CounterService, error) {
	var err error

	serverOpts := []protocol.Option{
		protocol.WithTopicTokenNamespace("ex:"),
		protocol.WithTopicTokens{
			"executorId": client.ClientID(),
		},
	}

	var executorOpts protocol.CommandExecutorOptions
	executorOpts.ApplyOptions(opts, serverOpts...)

	counterService := &CounterService{}

	counterService.ReadCounterCommandExecutor, err = NewReadCounterCommandExecutor(
		client,
		CommandTopic,
		readCounterHandler,
		&executorOpts,
	)
	if err != nil {
		return nil, err
	}

	counterService.IncrementCommandExecutor, err = NewIncrementCommandExecutor(
		client,
		CommandTopic,
		incrementHandler,
		&executorOpts,
	)
	if err != nil {
		return nil, err
	}

	counterService.ResetCommandExecutor, err = NewResetCommandExecutor(
		client,
		CommandTopic,
		resetHandler,
		&executorOpts,
	)
	if err != nil {
		return nil, err
	}

	return counterService, nil
}

func (service *CounterService) Listen(
	ctx context.Context,
) (func(), error) {
	return protocol.Listen(
		ctx,
		service.ReadCounterCommandExecutor,
		service.IncrementCommandExecutor,
		service.ResetCommandExecutor,
	)
}

func NewCounterClient(
	client mqtt.Client,
	opts ...protocol.Option,
) (*CounterClient, error) {
	var err error

	clientOpts := []protocol.Option{
		protocol.WithTopicTokenNamespace("ex:"),
		protocol.WithTopicTokens{
			"invokerClientId": client.ClientID(),
		},
	}

	var invokerOpts protocol.CommandInvokerOptions
	invokerOpts.ApplyOptions(opts, clientOpts...)

	counterClient := &CounterClient{}

	counterClient.ReadCounterCommandInvoker, err = NewReadCounterCommandInvoker(
		client,
		CommandTopic,
		&invokerOpts,
	)
	if err != nil {
		return nil, err
	}

	counterClient.IncrementCommandInvoker, err = NewIncrementCommandInvoker(
		client,
		CommandTopic,
		&invokerOpts,
	)
	if err != nil {
		return nil, err
	}

	counterClient.ResetCommandInvoker, err = NewResetCommandInvoker(
		client,
		CommandTopic,
		&invokerOpts,
	)
	if err != nil {
		return nil, err
	}

	return counterClient, nil
}

func (client *CounterClient) Listen(
	ctx context.Context,
) (func(), error) {
	return protocol.Listen(
		ctx,
		client.ReadCounterCommandInvoker,
		client.IncrementCommandInvoker,
		client.ResetCommandInvoker,
	)
}
