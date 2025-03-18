// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseCloudEvent struct {
	Source          *string `yaml:"source"`
	Type            *string `yaml:"type"`
	SpecVersion     *string `yaml:"spec-version"`
	ID              *string `yaml:"id"`
	Time            any     `yaml:"time"`
	DataContentType *string `yaml:"data-content-type"`
	Subject         any     `yaml:"subject"`
	DataSchema      any     `yaml:"data-schema"`
}

type TestCaseCloudEvent struct {
	testCaseCloudEvent
}

func (cloudEvent *TestCaseCloudEvent) UnmarshalYAML(
	node *yaml.Node,
) error {
	*cloudEvent = TestCaseCloudEvent{}

	cloudEvent.Time = false
	cloudEvent.Subject = false
	cloudEvent.DataSchema = false

	return node.Decode(&cloudEvent.testCaseCloudEvent)
}
