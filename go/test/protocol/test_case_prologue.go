// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCasePrologue struct {
	MqttConfig      TestCaseMqttConfig `yaml:"mqtt-config"`
	PushAcks        TestCasePushAcks   `yaml:"push-acks"`
	Executors       []TestCaseExecutor `yaml:"executors"`
	Invokers        []TestCaseInvoker  `yaml:"invokers"`
	Receivers       []TestCaseReceiver `yaml:"receivers"`
	Senders         []TestCaseSender   `yaml:"senders"`
	Catch           *TestCaseCatch     `yaml:"catch"`
	CountdownEvents map[string]int     `yaml:"countdown-events"`
}

type TestCasePrologue struct {
	testCasePrologue
}

func (prologue *TestCasePrologue) UnmarshalYAML(node *yaml.Node) error {
	*prologue = TestCasePrologue{}

	prologue.MqttConfig = MakeTestCaseMqttConfig()

	return node.Decode(&prologue.testCasePrologue)
}
