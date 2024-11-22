// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type TestCaseEpilogue struct {
	SubscribedTopics     []string                    `yaml:"subscribed-topics"`
	PublicationCount     *int                        `yaml:"publication-count"`
	PublishedMessages    []TestCasePublishedMessage  `yaml:"published-messages"`
	AcknowledgementCount *int                        `yaml:"acknowledgement-count"`
	ReceivedTelemetries  []TestCaseReceivedTelemetry `yaml:"received-telemetries"`
	ExecutionCount       *int                        `yaml:"execution-count"`
	ExecutionCounts      map[int]int                 `yaml:"execution-counts"`
	TelemetryCount       *int                        `yaml:"telemetry-count"`
	TelemetryCounts      map[int]int                 `yaml:"telemetry-counts"`
	Catch                *TestCaseCatch              `yaml:"catch"`
}
