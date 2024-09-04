/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type IncrementCommandExecutor struct {
	*protocol.CommandExecutor[any, IncrementCommandResponse]
}

func NewIncrementCommandExecutor(
	client mqtt.Client,
	requestTopic string,
	handler protocol.CommandHandler[any, IncrementCommandResponse],
	opt ...protocol.CommandExecutorOption,
) (*IncrementCommandExecutor, error) {
	var err error
	executor := &IncrementCommandExecutor{}

	var opts protocol.CommandExecutorOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"commandName": "increment",
		},
		protocol.WithIdempotent(false),
	)

	executor.CommandExecutor, err = protocol.NewCommandExecutor(
		client,
		protocol.Empty{},
		protocol.JSON[IncrementCommandResponse]{},
		requestTopic,
		handler,
		&opts,
	)

	return executor, err
}
