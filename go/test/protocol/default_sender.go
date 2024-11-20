// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultSender struct {
	TelemetryName  *string `toml:"telemetry-name"`
	TelemetryTopic *string `toml:"telemetry-topic"`
	ModelID        *string `toml:"model-id"`
	TopicNamespace *string `toml:"topic-namespace"`
}

func (sender *DefaultSender) GetTelemetryName() *string {
	if sender.TelemetryName == nil {
		return nil
	}

	telemetryName := *sender.TelemetryName
	return &telemetryName
}

func (sender *DefaultSender) GetTelemetryTopic() *string {
	if sender.TelemetryTopic == nil {
		return nil
	}

	telemetryTopic := *sender.TelemetryTopic
	return &telemetryTopic
}

func (sender *DefaultSender) GetModelID() *string {
	if sender.ModelID == nil {
		return nil
	}

	modelID := *sender.ModelID
	return &modelID
}

func (sender *DefaultSender) GetTopicNamespace() *string {
	if sender.TopicNamespace == nil {
		return nil
	}

	topicNamespace := *sender.TopicNamespace
	return &topicNamespace
}
