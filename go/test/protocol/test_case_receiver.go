// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseReceiver struct {
	TelemetryName  *string           `yaml:"telemetry-name"`
	TelemetryTopic *string           `yaml:"telemetry-topic"`
	ModelID        *string           `yaml:"model-id"`
	TopicNamespace *string           `yaml:"topic-namespace"`
	CustomTokenMap map[string]string `yaml:"custom-token-map"`
	RaiseError     TestCaseError     `yaml:"raise-error"`
}

type TestCaseReceiver struct {
	testCaseReceiver
}

func (receiver *TestCaseReceiver) UnmarshalYAML(node *yaml.Node) error {
	*receiver = TestCaseReceiver{}

	receiver.TelemetryName = TestCaseDefaultInfo.Prologue.Receiver.GetTelemetryName()
	receiver.TelemetryTopic = TestCaseDefaultInfo.Prologue.Receiver.GetTelemetryTopic()
	receiver.ModelID = TestCaseDefaultInfo.Prologue.Receiver.GetModelID()
	receiver.TopicNamespace = TestCaseDefaultInfo.Prologue.Receiver.GetTopicNamespace()

	return node.Decode(&receiver.testCaseReceiver)
}
