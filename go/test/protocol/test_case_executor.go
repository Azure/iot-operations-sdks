// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"gopkg.in/yaml.v3"
)

type testCaseExecutor struct {
	CommandName          *string             `yaml:"command-name"`
	RequestTopic         *string             `yaml:"request-topic"`
	ModelID              *string             `yaml:"model-id"`
	ExecutorID           *string             `yaml:"executor-id"`
	TopicNamespace       *string             `yaml:"topic-namespace"`
	Idempotent           bool                `yaml:"idempotent"`
	CacheableDuration    *TestCaseDuration   `yaml:"cacheable-duration"`
	ExecutionTimeout     *TestCaseDuration   `yaml:"execution-timeout"`
	RequestResponsesMap  map[string][]string `yaml:"request-responses-map"`
	ResponseMetadata     map[string]*string  `yaml:"response-metadata"`
	ExecutionConcurrency *uint               `yaml:"execution-concurrency"`
	RaiseError           TestCaseError       `yaml:"raise-error"`
	Sync                 []TestCaseSync      `yaml:"sync"`
}

type TestCaseExecutor struct {
	testCaseExecutor
}

func (executor *TestCaseExecutor) UnmarshalYAML(node *yaml.Node) error {
	*executor = TestCaseExecutor{}

	executor.CommandName = TestCaseDefaultInfo.Prologue.Executor.GetCommandName()
	executor.RequestTopic = TestCaseDefaultInfo.Prologue.Executor.GetRequestTopic()
	executor.ModelID = TestCaseDefaultInfo.Prologue.Executor.GetModelID()
	executor.ExecutorID = TestCaseDefaultInfo.Prologue.Executor.GetExecutorID()
	executor.TopicNamespace = TestCaseDefaultInfo.Prologue.Executor.GetTopicNamespace()
	executor.Idempotent = TestCaseDefaultInfo.Prologue.Executor.GetIdempotent()
	executor.RequestResponsesMap = TestCaseDefaultInfo.Prologue.Executor.GetRequestResponsesMap()
	executor.ExecutionConcurrency = TestCaseDefaultInfo.Prologue.Executor.GetExecutionConcurrency()

	err := node.Decode(&executor.testCaseExecutor)

	if executor.CacheableDuration == nil {
		defaultCacheableDuration := TestCaseDefaultInfo.Prologue.Executor.GetCacheableDuration()
		executor.CacheableDuration = &defaultCacheableDuration
	}

	if executor.ExecutionTimeout == nil {
		defaultExecutionTimeout := TestCaseDefaultInfo.Prologue.Executor.GetExecutionTimeout()
		executor.ExecutionTimeout = &defaultExecutionTimeout
	}

	return err
}
