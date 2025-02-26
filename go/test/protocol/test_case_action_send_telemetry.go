// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseActionSendTelemetry struct {
	TelemetryName  *string             `yaml:"telemetry-name"`
	TopicTokenMap  map[string]string   `yaml:"topic-token-map"`
	Timeout        *TestCaseDuration   `yaml:"timeout"`
	TelemetryValue *string             `yaml:"telemetry-value"`
	Metadata       *map[string]string  `yaml:"metadata"`
	CloudEvent     *TestCaseCloudEvent `yaml:"cloud-event"`
	Qos            *int                `yaml:"qos"`
}

type TestCaseActionSendTelemetry struct {
	testCaseActionSendTelemetry
}

func (sendTelemetry *TestCaseActionSendTelemetry) UnmarshalYAML(
	node *yaml.Node,
) error {
	*sendTelemetry = TestCaseActionSendTelemetry{}

	sendTelemetry.TelemetryName = TestCaseDefaultInfo.Actions.SendTelemetry.GetTelemetryName()
	sendTelemetry.TelemetryValue = TestCaseDefaultInfo.Actions.SendTelemetry.GetTelemetryValue()
	sendTelemetry.Qos = TestCaseDefaultInfo.Actions.SendTelemetry.GetQos()

	err := node.Decode(&sendTelemetry.testCaseActionSendTelemetry)

	if sendTelemetry.Timeout == nil {
		defaultTimeout := TestCaseDefaultInfo.Actions.SendTelemetry.GetTimeout()
		sendTelemetry.Timeout = &defaultTimeout
	}

	return err
}
