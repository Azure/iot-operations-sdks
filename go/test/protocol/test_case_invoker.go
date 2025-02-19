// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseInvoker struct {
	CommandName         *string             `yaml:"command-name"`
	Serializer          TestCaseSerializer  `yaml:"serializer"`
	RequestTopic        *string             `yaml:"request-topic"`
	TopicNamespace      *string             `yaml:"topic-namespace"`
	ResponseTopicPrefix *string             `yaml:"response-topic-prefix"`
	ResponseTopicSuffix *string             `yaml:"response-topic-suffix"`
	TopicTokenMap       map[string]string   `yaml:"topic-token-map"`
	ResponseTopicMap    *map[string]*string `yaml:"response-topic-map"`
}

type TestCaseInvoker struct {
	testCaseInvoker
}

func (invoker *TestCaseInvoker) UnmarshalYAML(node *yaml.Node) error {
	*invoker = TestCaseInvoker{}

	invoker.CommandName = TestCaseDefaultInfo.Prologue.Invoker.GetCommandName()
	invoker.Serializer = TestCaseDefaultInfo.Prologue.Invoker.GetSerializer()
	invoker.RequestTopic = TestCaseDefaultInfo.Prologue.Invoker.GetRequestTopic()
	invoker.TopicNamespace = TestCaseDefaultInfo.Prologue.Invoker.GetTopicNamespace()
	invoker.ResponseTopicPrefix = TestCaseDefaultInfo.Prologue.Invoker.GetResponseTopicPrefix()
	invoker.ResponseTopicSuffix = TestCaseDefaultInfo.Prologue.Invoker.GetResponseTopicSuffix()

	return node.Decode(&invoker.testCaseInvoker)
}
