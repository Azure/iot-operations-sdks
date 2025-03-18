// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import "gopkg.in/yaml.v3"

const (
	HeaderNameKey         = "header-name"
	HeaderValueKey        = "header-value"
	TimeoutNameKey        = "timeout-name"
	TimeoutValueKey       = "timeout-value"
	PropertyNameKey       = "property-name"
	PropertyValueKey      = "property-value"
	ProtocolVersionKey    = "protocol-version"
	SupportedProtocolsKey = "supported-protocols"
)

type testCaseCatch struct {
	ErrorKind    string            `yaml:"error-kind"`
	IsShallow    *bool             `yaml:"is-shallow"`
	IsRemote     *bool             `yaml:"is-remote"`
	Message      *string           `yaml:"message"`
	Supplemental map[string]string `yaml:"supplemental"`
}

type TestCaseCatch struct {
	testCaseCatch
}

func (catch *TestCaseCatch) UnmarshalYAML(node *yaml.Node) error {
	*catch = TestCaseCatch{}
	return node.Decode(&catch.testCaseCatch)
}
