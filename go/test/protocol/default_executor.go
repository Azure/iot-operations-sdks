// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultExecutor struct {
	CommandName          *string             `toml:"command-name"`
	Serializer           DefaultSerializer   `toml:"serializer"`
	RequestTopic         *string             `toml:"request-topic"`
	ExecutorID           *string             `toml:"executor-id"`
	TopicNamespace       *string             `toml:"topic-namespace"`
	Idempotent           bool                `toml:"idempotent"`
	ExecutionTimeout     TestCaseDuration    `toml:"execution-timeout"`
	RequestResponsesMap  map[string][]string `toml:"request-responses-map"`
	ExecutionConcurrency *uint               `toml:"execution-concurrency"`
}

func (executor *DefaultExecutor) GetCommandName() *string {
	if executor.CommandName == nil {
		return nil
	}

	commandName := *executor.CommandName
	return &commandName
}

func (executor *DefaultExecutor) GetSerializer() TestCaseSerializer {
	return executor.Serializer.GetSerializer()
}

func (executor *DefaultExecutor) GetRequestTopic() *string {
	if executor.RequestTopic == nil {
		return nil
	}

	requestTopic := *executor.RequestTopic
	return &requestTopic
}

func (executor *DefaultExecutor) GetExecutorID() *string {
	if executor.ExecutorID == nil {
		return nil
	}

	executorID := *executor.ExecutorID
	return &executorID
}

func (executor *DefaultExecutor) GetTopicNamespace() *string {
	if executor.TopicNamespace == nil {
		return nil
	}

	topicNamespace := *executor.TopicNamespace
	return &topicNamespace
}

func (executor *DefaultExecutor) GetIdempotent() bool {
	return executor.Idempotent
}

func (executor *DefaultExecutor) GetExecutionTimeout() TestCaseDuration {
	return executor.ExecutionTimeout
}

func (executor *DefaultExecutor) GetRequestResponsesMap() map[string][]string {
	requestResponsesMap := make(map[string][]string)

	for k, v := range executor.RequestResponsesMap {
		responses := make([]string, len(v))
		copy(responses, v)
		requestResponsesMap[k] = responses
	}

	return requestResponsesMap
}

func (executor *DefaultExecutor) GetExecutionConcurrency() *uint {
	if executor.ExecutionConcurrency == nil {
		return nil
	}

	executionConcurrency := *executor.ExecutionConcurrency
	return &executionConcurrency
}
