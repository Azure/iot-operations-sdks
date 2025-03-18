// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseSerializer struct {
	OutContentType        *string  `yaml:"out-content-type"`
	AcceptContentTypes    []string `yaml:"accept-content-types"`
	IndicateCharacterData bool     `yaml:"indicate-character-data"`
	AllowCharacterData    bool     `yaml:"allow-character-data"`
	FailDeserialization   bool     `yaml:"fail-deserialization"`
}

type TestCaseSerializer struct {
	testCaseSerializer
}

func (serializer *TestCaseSerializer) UnmarshalYAML(node *yaml.Node) error {
	*serializer = TestCaseDefaultSerializer.GetSerializer()

	return node.Decode(&serializer.testCaseSerializer)
}
