/* This is an auto-generated file.  Do not modify. */
package dtmi_com_example_Counter__1

import (
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type ReadCounterCommandExecutor struct {
	*protocol.CommandExecutor[any, ReadCounterCommandResponse]
}

func NewReadCounterCommandExecutor(
	client mqtt.Client,
	requestTopic string,
	handler protocol.CommandHandler[any, ReadCounterCommandResponse],
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
		protocol.JSON[ReadCounterCommandResponse]{},
		requestTopic,
		handler,
		&opts,
	)

	return executor, err
}
