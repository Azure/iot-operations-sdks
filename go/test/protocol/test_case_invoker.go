package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseInvoker struct {
	CommandName         *string             `yaml:"command-name"`
	RequestTopic        *string             `yaml:"request-topic"`
	ModelID             *string             `yaml:"model-id"`
	TopicNamespace      *string             `yaml:"topic-namespace"`
	ResponseTopicPrefix *string             `yaml:"response-topic-prefix"`
	ResponseTopicSuffix *string             `yaml:"response-topic-suffix"`
	CustomTokenMap      map[string]string   `yaml:"custom-token-map"`
	ResponseTopicMap    *map[string]*string `yaml:"response-topic-map"`
}

type TestCaseInvoker struct {
	testCaseInvoker
}

func (invoker *TestCaseInvoker) UnmarshalYAML(node *yaml.Node) error {
	*invoker = TestCaseInvoker{}

	invoker.CommandName = TestCaseDefaultInfo.Prologue.Invoker.GetCommandName()
	invoker.RequestTopic = TestCaseDefaultInfo.Prologue.Invoker.GetRequestTopic()
	invoker.ModelID = TestCaseDefaultInfo.Prologue.Invoker.GetModelID()
	invoker.ResponseTopicPrefix = TestCaseDefaultInfo.Prologue.Invoker.GetResponseTopicPrefix()
	invoker.ResponseTopicSuffix = TestCaseDefaultInfo.Prologue.Invoker.GetResponseTopicSuffix()

	return node.Decode(&invoker.testCaseInvoker)
}
