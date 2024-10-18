// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"errors"

	"gopkg.in/yaml.v3"
)

type ActionKind int

const (
	AwaitAck ActionKind = iota
	AwaitInvocation
	AwaitPublish
	Disconnect
	FreezeTime
	InvokeCommand
	ReceiveRequest
	ReceiveResponse
	Sleep
	Sync
	UnfreezeTime
)

type TestCaseAction struct {
	Kind   ActionKind
	Action any
}

func (action *TestCaseAction) AsAwaitAck() *TestCaseActionAwaitAck {
	if actionAwaitAck, ok := (action.Action).(TestCaseActionAwaitAck); ok {
		return &actionAwaitAck
	}
	return nil
}

func (action *TestCaseAction) AsAwaitInvocation() *TestCaseActionAwaitInvocation {
	if actionAwaitInvocation, ok := (action.Action).(testCaseActionAwaitInvocation); ok {
		return &TestCaseActionAwaitInvocation{
			testCaseActionAwaitInvocation: actionAwaitInvocation,
		}
	}
	return nil
}

func (action *TestCaseAction) AsAwaitPublish() *TestCaseActionAwaitPublish {
	if actionAwaitPublish, ok := (action.Action).(TestCaseActionAwaitPublish); ok {
		return &actionAwaitPublish
	}
	return nil
}

func (action *TestCaseAction) AsInvokeCommand() *TestCaseActionInvokeCommand {
	if actionInvokeCommand, ok := (action.Action).(testCaseActionInvokeCommand); ok {
		return &TestCaseActionInvokeCommand{
			testCaseActionInvokeCommand: actionInvokeCommand,
		}
	}
	return nil
}

func (action *TestCaseAction) AsReceiveRequest() *TestCaseActionReceiveRequest {
	if actionReceiveRequest, ok := (action.Action).(testCaseActionReceiveRequest); ok {
		return &TestCaseActionReceiveRequest{
			testCaseActionReceiveRequest: actionReceiveRequest,
		}
	}
	return nil
}

func (action *TestCaseAction) AsReceiveResponse() *TestCaseActionReceiveResponse {
	if actionReceiveResponse, ok := (action.Action).(testCaseActionReceiveResponse); ok {
		return &TestCaseActionReceiveResponse{
			testCaseActionReceiveResponse: actionReceiveResponse,
		}
	}
	return nil
}

func (action *TestCaseAction) AsSleep() *TestCaseActionSleep {
	if actionSleep, ok := (action.Action).(TestCaseActionSleep); ok {
		return &actionSleep
	}
	return nil
}

func (action *TestCaseAction) AsSync() *TestCaseActionSync {
	if actionSync, ok := (action.Action).(TestCaseActionSync); ok {
		return &actionSync
	}
	return nil
}

type testCaseActionKind struct {
	Action string `yaml:"action"`
}

func (action *TestCaseAction) UnmarshalYAML(node *yaml.Node) error {
	*action = TestCaseAction{}

	var actionKind testCaseActionKind
	err := node.Decode(&actionKind)
	if err != nil {
		return err
	}

	switch actionKind.Action {
	default:
		return errors.New("unrecognized TestCaseAction kind")
	case "await acknowledgement":
		action.Kind = AwaitAck
		var awaitAck TestCaseActionAwaitAck
		err = node.Decode(&awaitAck)
		action.Action = awaitAck
	case "await invocation":
		action.Kind = AwaitInvocation
		var awaitInvocation TestCaseActionAwaitInvocation
		err = node.Decode(&awaitInvocation)
		action.Action = awaitInvocation.testCaseActionAwaitInvocation
	case "await publish":
		action.Kind = AwaitPublish
		var awaitPublish TestCaseActionAwaitPublish
		err = node.Decode(&awaitPublish)
		action.Action = awaitPublish
	case "disconnect":
		action.Kind = Disconnect
		return nil
	case "freeze time":
		action.Kind = FreezeTime
		return nil
	case "invoke command":
		action.Kind = InvokeCommand
		var invokeCommand TestCaseActionInvokeCommand
		err = node.Decode(&invokeCommand)
		action.Action = invokeCommand.testCaseActionInvokeCommand
	case "receive request":
		action.Kind = ReceiveRequest
		var receiveRequest TestCaseActionReceiveRequest
		err = node.Decode(&receiveRequest)
		action.Action = receiveRequest.testCaseActionReceiveRequest
	case "receive response":
		action.Kind = ReceiveResponse
		var receiveResponse TestCaseActionReceiveResponse
		err = node.Decode(&receiveResponse)
		action.Action = receiveResponse.testCaseActionReceiveResponse
	case "sleep":
		action.Kind = Sleep
		var sleep TestCaseActionSleep
		err = node.Decode(&sleep)
		action.Action = sleep
	case "sync":
		action.Kind = Sync
		var sync TestCaseActionSync
		err = node.Decode(&sync)
		action.Action = sync
	case "unfreeze time":
		action.Kind = UnfreezeTime
		return nil
	}

	return err
}
