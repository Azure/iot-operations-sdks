// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseReceiver struct {
	Serializer     TestCaseSerializer `yaml:"serializer"`
	TelemetryTopic *string            `yaml:"telemetry-topic"`
	TopicNamespace *string            `yaml:"topic-namespace"`
	TopicTokenMap  map[string]string  `yaml:"topic-token-map"`
	RaiseError     TestCaseError      `yaml:"raise-error"`
}

type TestCaseReceiver struct {
	testCaseReceiver
}

func (receiver *TestCaseReceiver) UnmarshalYAML(node *yaml.Node) error {
	*receiver = TestCaseReceiver{}

	receiver.Serializer = TestCaseDefaultInfo.Prologue.Receiver.GetSerializer()
	receiver.TelemetryTopic = TestCaseDefaultInfo.Prologue.Receiver.GetTelemetryTopic()
	receiver.TopicNamespace = TestCaseDefaultInfo.Prologue.Receiver.GetTopicNamespace()

	return node.Decode(&receiver.testCaseReceiver)
}
