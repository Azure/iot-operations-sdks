// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCasePublishedMessage struct {
	CorrelationIndex   *int               `yaml:"correlation-index"`
	Topic              *string            `yaml:"topic"`
	Payload            any                `yaml:"payload"`
	ContentType        *string            `yaml:"content-type"`
	FormatIndicator    *int               `yaml:"format-indicator"`
	Metadata           map[string]*string `yaml:"metadata"`
	CommandStatus      any                `yaml:"command-status"`
	IsApplicationError *bool              `yaml:"is-application-error"`
	SourceID           *string            `yaml:"source-id"`
	Expiry             *uint32            `yaml:"expiry"`
}

type TestCasePublishedMessage struct {
	testCasePublishedMessage
}

func (publishedMessage *TestCasePublishedMessage) UnmarshalYAML(
	node *yaml.Node,
) error {
	*publishedMessage = TestCasePublishedMessage{}

	publishedMessage.Payload = false
	publishedMessage.CommandStatus = false

	return node.Decode(&publishedMessage.testCasePublishedMessage)
}
