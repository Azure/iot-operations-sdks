// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultInvokeCommand struct {
	CommandName  *string          `toml:"command-name"`
	RequestValue *string          `toml:"request-value"`
	Timeout      TestCaseDuration `toml:"timeout"`
}

func (invokeCommand *DefaultInvokeCommand) GetCommandName() *string {
	if invokeCommand.CommandName == nil {
		return nil
	}

	commandName := *invokeCommand.CommandName
	return &commandName
}

func (invokeCommand *DefaultInvokeCommand) GetRequestValue() *string {
	if invokeCommand.RequestValue == nil {
		return nil
	}

	requestValue := *invokeCommand.RequestValue
	return &requestValue
}

func (invokeCommand *DefaultInvokeCommand) GetTimeout() TestCaseDuration {
	return invokeCommand.Timeout
}
