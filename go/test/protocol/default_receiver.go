// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultReceiver struct {
	TelemetryName  *string `toml:"telemetry-name"`
	TelemetryTopic *string `toml:"telemetry-topic"`
	ModelID        *string `toml:"model-id"`
	TopicNamespace *string `toml:"topic-namespace"`
}

func (receiver *DefaultReceiver) GetTelemetryName() *string {
	if receiver.TelemetryName == nil {
		return nil
	}

	telemetryName := *receiver.TelemetryName
	return &telemetryName
}

func (receiver *DefaultReceiver) GetTelemetryTopic() *string {
	if receiver.TelemetryTopic == nil {
		return nil
	}

	telemetryTopic := *receiver.TelemetryTopic
	return &telemetryTopic
}

func (receiver *DefaultReceiver) GetModelID() *string {
	if receiver.ModelID == nil {
		return nil
	}

	modelID := *receiver.ModelID
	return &modelID
}

func (receiver *DefaultReceiver) GetTopicNamespace() *string {
	if receiver.TopicNamespace == nil {
		return nil
	}

	topicNamespace := *receiver.TopicNamespace
	return &topicNamespace
}
