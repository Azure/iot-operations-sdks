// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultReceiveTelemetry struct {
	Topic           *string          `toml:"topic"`
	Payload         *string          `toml:"payload"`
	ContentType     *string          `toml:"content-type"`
	FormatIndicator *int             `toml:"format-indicator"`
	Qos             *int             `toml:"qos"`
	MessageExpiry   TestCaseDuration `toml:"message-expiry"`
	SourceIndex     *int             `toml:"source-index"`
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetTopic() *string {
	if receiveTelemetry.Topic == nil {
		return nil
	}

	topic := *receiveTelemetry.Topic
	return &topic
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetPayload() *string {
	if receiveTelemetry.Payload == nil {
		return nil
	}

	payload := *receiveTelemetry.Payload
	return &payload
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetContentType() *string {
	if receiveTelemetry.ContentType == nil {
		return nil
	}

	contentType := *receiveTelemetry.ContentType
	return &contentType
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetFormatIndicator() *int {
	if receiveTelemetry.FormatIndicator == nil {
		return nil
	}

	formatIndicator := *receiveTelemetry.FormatIndicator
	return &formatIndicator
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetQos() *int {
	if receiveTelemetry.Qos == nil {
		return nil
	}

	qos := *receiveTelemetry.Qos
	return &qos
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetMessageExpiry() TestCaseDuration {
	return receiveTelemetry.MessageExpiry
}

func (receiveTelemetry *DefaultReceiveTelemetry) GetSourceIndex() *int {
	if receiveTelemetry.SourceIndex == nil {
		return nil
	}

	senderIndex := *receiveTelemetry.SourceIndex
	return &senderIndex
}
