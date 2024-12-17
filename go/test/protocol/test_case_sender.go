// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseSender struct {
	TelemetryName  *string           `yaml:"telemetry-name"`
	TelemetryTopic *string           `yaml:"telemetry-topic"`
	ModelID        *string           `yaml:"model-id"`
	TopicNamespace *string           `yaml:"topic-namespace"`
	CustomTokenMap map[string]string `yaml:"custom-token-map"`
}

type TestCaseSender struct {
	testCaseSender
}

func (sender *TestCaseSender) UnmarshalYAML(node *yaml.Node) error {
	*sender = TestCaseSender{}

	sender.TelemetryName = TestCaseDefaultInfo.Prologue.Sender.GetTelemetryName()
	sender.TelemetryTopic = TestCaseDefaultInfo.Prologue.Sender.GetTelemetryTopic()
	sender.ModelID = TestCaseDefaultInfo.Prologue.Sender.GetModelID()
	sender.TopicNamespace = TestCaseDefaultInfo.Prologue.Sender.GetTopicNamespace()

	return node.Decode(&sender.testCaseSender)
}
