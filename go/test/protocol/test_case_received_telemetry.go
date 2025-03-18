// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type TestCaseCloudEventCapsule struct {
	CloudEvent *TestCaseCloudEvent `yaml:"cloud-event"`
}

type testCaseReceivedTelemetry struct {
	TelemetryValue any                        `yaml:"telemetry-value"`
	Metadata       *map[string]string         `yaml:"metadata"`
	TopicTokens    *map[string]string         `yaml:"topic-tokens"`
	Capsule        *TestCaseCloudEventCapsule `yaml:",inline"`
	SourceIndex    *int                       `yaml:"source-index"`
}

type TestCaseReceivedTelemetry struct {
	testCaseReceivedTelemetry
}

func (receivedTelemetry *TestCaseReceivedTelemetry) UnmarshalYAML(
	node *yaml.Node,
) error {
	*receivedTelemetry = TestCaseReceivedTelemetry{}

	receivedTelemetry.TelemetryValue = false

	return node.Decode(&receivedTelemetry.testCaseReceivedTelemetry)
}
