// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseActionAwaitInvocation struct {
	InvocationIndex int                `yaml:"invocation-index"`
	ResponseValue   any                `yaml:"response-value"`
	Metadata        *map[string]string `yaml:"metadata"`
	Catch           *TestCaseCatch     `yaml:"catch"`
}

type TestCaseActionAwaitInvocation struct {
	testCaseActionAwaitInvocation
}

func (actionAwaitInvocation *TestCaseActionAwaitInvocation) UnmarshalYAML(
	node *yaml.Node,
) error {
	*actionAwaitInvocation = TestCaseActionAwaitInvocation{}

	actionAwaitInvocation.ResponseValue = false

	return node.Decode(&actionAwaitInvocation.testCaseActionAwaitInvocation)
}
