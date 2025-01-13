// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseSender struct {
	TelemetryName  *string           `yaml:"telemetry-name"`
	TelemetryTopic *string           `yaml:"telemetry-topic"`
	DataSchema     *string           `yaml:"data-schema"`
	TopicNamespace *string           `yaml:"topic-namespace"`
	TopicTokenMap  map[string]string `yaml:"topic-token-map"`
}

type TestCaseSender struct {
	testCaseSender
}

func (sender *TestCaseSender) UnmarshalYAML(node *yaml.Node) error {
	*sender = TestCaseSender{}

	sender.TelemetryName = TestCaseDefaultInfo.Prologue.Sender.GetTelemetryName()
	sender.TelemetryTopic = TestCaseDefaultInfo.Prologue.Sender.GetTelemetryTopic()
	sender.DataSchema = TestCaseDefaultInfo.Prologue.Sender.GetDataSchema()
	sender.TopicNamespace = TestCaseDefaultInfo.Prologue.Sender.GetTopicNamespace()

	return node.Decode(&sender.testCaseSender)
}
