package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseActionInvokeCommand struct {
	InvocationIndex int                `yaml:"invocation-index"`
	CommandName     *string            `yaml:"command-name"`
	ExecutorID      *string            `yaml:"executor-id"`
	Timeout         *TestCaseDuration  `yaml:"timeout"`
	RequestValue    *string            `yaml:"request-value"`
	Metadata        *map[string]string `yaml:"metadata"`
}

type TestCaseActionInvokeCommand struct {
	testCaseActionInvokeCommand
}

func (invokeCommand *TestCaseActionInvokeCommand) UnmarshalYAML(
	node *yaml.Node,
) error {
	*invokeCommand = TestCaseActionInvokeCommand{}

	invokeCommand.CommandName = TestCaseDefaultInfo.Actions.InvokeCommand.GetCommandName()
	invokeCommand.ExecutorID = TestCaseDefaultInfo.Actions.InvokeCommand.GetExecutorID()
	invokeCommand.RequestValue = TestCaseDefaultInfo.Actions.InvokeCommand.GetRequestValue()

	err := node.Decode(&invokeCommand.testCaseActionInvokeCommand)

	if invokeCommand.Timeout == nil {
		defaultTimeout := TestCaseDefaultInfo.Actions.InvokeCommand.GetTimeout()
		invokeCommand.Timeout = &defaultTimeout
	}

	return err
}
