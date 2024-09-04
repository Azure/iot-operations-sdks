/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type ResetCommandExecutor struct {
	*protocol.CommandExecutor[any, any]
}

func NewResetCommandExecutor(
	client mqtt.Client,
	requestTopic string,
	handler protocol.CommandHandler[any, any],
	opt ...protocol.CommandExecutorOption,
) (*ResetCommandExecutor, error) {
	var err error
	executor := &ResetCommandExecutor{}

	var opts protocol.CommandExecutorOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"commandName": "reset",
		},
		protocol.WithIdempotent(false),
	)

	executor.CommandExecutor, err = protocol.NewCommandExecutor(
		client,
		protocol.Empty{},
		protocol.Empty{},
		requestTopic,
		handler,
		&opts,
	)

	return executor, err
}
