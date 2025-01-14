// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultReceiver struct {
	TelemetryTopic *string `toml:"telemetry-topic"`
	TopicNamespace *string `toml:"topic-namespace"`
}

func (receiver *DefaultReceiver) GetTelemetryTopic() *string {
	if receiver.TelemetryTopic == nil {
		return nil
	}

	telemetryTopic := *receiver.TelemetryTopic
	return &telemetryTopic
}

func (receiver *DefaultReceiver) GetTopicNamespace() *string {
	if receiver.TopicNamespace == nil {
		return nil
	}

	topicNamespace := *receiver.TopicNamespace
	return &topicNamespace
}
