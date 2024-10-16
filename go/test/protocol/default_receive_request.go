// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultReceiveRequest struct {
	Topic            *string          `toml:"topic"`
	Payload          *string          `toml:"payload"`
	ContentType      *string          `toml:"content-type"`
	FormatIndicator  *int             `toml:"format-indicator"`
	CorrelationIndex *int             `toml:"correlation-index"`
	Qos              *int             `toml:"qos"`
	MessageExpiry    TestCaseDuration `toml:"message-expiry"`
	ResponseTopic    *string          `toml:"response-topic"`
	InvokerIndex     *int             `toml:"invoker-index"`
}

func (receiveRequest *DefaultReceiveRequest) GetTopic() *string {
	if receiveRequest.Topic == nil {
		return nil
	}

	topic := *receiveRequest.Topic
	return &topic
}

func (receiveRequest *DefaultReceiveRequest) GetPayload() *string {
	if receiveRequest.Payload == nil {
		return nil
	}

	payload := *receiveRequest.Payload
	return &payload
}

func (receiveRequest *DefaultReceiveRequest) GetContentType() *string {
	if receiveRequest.ContentType == nil {
		return nil
	}

	contentType := *receiveRequest.ContentType
	return &contentType
}

func (receiveRequest *DefaultReceiveRequest) GetFormatIndicator() *int {
	if receiveRequest.FormatIndicator == nil {
		return nil
	}

	formatIndicator := *receiveRequest.FormatIndicator
	return &formatIndicator
}

func (receiveRequest *DefaultReceiveRequest) GetCorrelationIndex() *int {
	if receiveRequest.CorrelationIndex == nil {
		return nil
	}

	correlationIndex := *receiveRequest.CorrelationIndex
	return &correlationIndex
}

func (receiveRequest *DefaultReceiveRequest) GetQos() *int {
	if receiveRequest.Qos == nil {
		return nil
	}

	qos := *receiveRequest.Qos
	return &qos
}

func (receiveRequest *DefaultReceiveRequest) GetMessageExpiry() TestCaseDuration {
	return receiveRequest.MessageExpiry
}

func (receiveRequest *DefaultReceiveRequest) GetResponseTopic() *string {
	if receiveRequest.ResponseTopic == nil {
		return nil
	}

	responseTopic := *receiveRequest.ResponseTopic
	return &responseTopic
}

func (receiveRequest *DefaultReceiveRequest) GetInvokerIndex() *int {
	if receiveRequest.InvokerIndex == nil {
		return nil
	}

	invokerIndex := *receiveRequest.InvokerIndex
	return &invokerIndex
}
