// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseActionReceiveResponse struct {
	Topic                *string           `yaml:"topic"`
	Payload              *string           `yaml:"payload"`
	ContentType          *string           `yaml:"content-type"`
	FormatIndicator      *int              `yaml:"format-indicator"`
	Metadata             map[string]string `yaml:"metadata"`
	CorrelationIndex     *int              `yaml:"correlation-index"`
	Qos                  *int              `yaml:"qos"`
	MessageExpiry        *TestCaseDuration `yaml:"message-expiry"`
	Status               *string           `yaml:"status"`
	StatusMessage        *string           `yaml:"status-message"`
	IsApplicationError   *string           `yaml:"is-application-error"`
	InvalidPropertyName  *string           `yaml:"invalid-property-name"`
	InvalidPropertyValue *string           `yaml:"invalid-property-value"`
	PacketIndex          *int              `yaml:"packet-index"`
}

type TestCaseActionReceiveResponse struct {
	testCaseActionReceiveResponse
}

func (receiveResponse *TestCaseActionReceiveResponse) UnmarshalYAML(
	node *yaml.Node,
) error {
	*receiveResponse = TestCaseActionReceiveResponse{}

	receiveResponse.Topic = TestCaseDefaultInfo.Actions.ReceiveResponse.GetTopic()
	receiveResponse.Payload = TestCaseDefaultInfo.Actions.ReceiveResponse.GetPayload()
	receiveResponse.ContentType = TestCaseDefaultInfo.Actions.ReceiveResponse.GetContentType()
	receiveResponse.FormatIndicator = TestCaseDefaultInfo.Actions.ReceiveResponse.GetFormatIndicator()
	receiveResponse.CorrelationIndex = TestCaseDefaultInfo.Actions.ReceiveResponse.GetCorrelationIndex()
	receiveResponse.Qos = TestCaseDefaultInfo.Actions.ReceiveResponse.GetQos()
	receiveResponse.Status = TestCaseDefaultInfo.Actions.ReceiveResponse.GetStatus()
	receiveResponse.StatusMessage = TestCaseDefaultInfo.Actions.ReceiveResponse.GetStatusMessage()
	receiveResponse.IsApplicationError = TestCaseDefaultInfo.Actions.ReceiveResponse.GetIsApplicationError()
	receiveResponse.InvalidPropertyName = TestCaseDefaultInfo.Actions.ReceiveResponse.GetInvalidPropertyName()
	receiveResponse.InvalidPropertyValue = TestCaseDefaultInfo.Actions.ReceiveResponse.GetInvalidPropertyValue()

	defaultMessageExpiry := TestCaseDefaultInfo.Actions.ReceiveResponse.GetMessageExpiry()
	receiveResponse.MessageExpiry = &defaultMessageExpiry

	return node.Decode(&receiveResponse.testCaseActionReceiveResponse)
}
