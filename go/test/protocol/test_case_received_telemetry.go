// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseReceivedTelemetry struct {
	TelemetryValue any                 `yaml:"telemetry-value"`
	Metadata       *map[string]string  `yaml:"metadata"`
	CloudEvent     *TestCaseCloudEvent `yaml:"cloud-event"`
	SourceIndex    *int                `yaml:"source-index"`
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
