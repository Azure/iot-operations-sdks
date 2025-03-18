// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseActionReceiveTelemetry struct {
	Topic           *string           `yaml:"topic"`
	Payload         *string           `yaml:"payload"`
	ContentType     *string           `yaml:"content-type"`
	FormatIndicator *int              `yaml:"format-indicator"`
	Metadata        map[string]string `yaml:"metadata"`
	Qos             *int              `yaml:"qos"`
	MessageExpiry   *TestCaseDuration `yaml:"message-expiry"`
	SourceIndex     *int              `yaml:"source-index"`
	PacketIndex     *int              `yaml:"packet-index"`
}

type TestCaseActionReceiveTelemetry struct {
	testCaseActionReceiveTelemetry
}

func (receiveTelemetry *TestCaseActionReceiveTelemetry) UnmarshalYAML(
	node *yaml.Node,
) error {
	*receiveTelemetry = TestCaseActionReceiveTelemetry{}

	receiveTelemetry.Topic = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetTopic()
	receiveTelemetry.Payload = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetPayload()
	receiveTelemetry.ContentType = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetContentType()
	receiveTelemetry.FormatIndicator = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetFormatIndicator()
	receiveTelemetry.Qos = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetQos()
	receiveTelemetry.SourceIndex = TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetSourceIndex()

	defaultMessageExpiry := TestCaseDefaultInfo.Actions.ReceiveTelemetry.GetMessageExpiry()
	receiveTelemetry.MessageExpiry = &defaultMessageExpiry

	return node.Decode(&receiveTelemetry.testCaseActionReceiveTelemetry)
}
