// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultReceiveResponse struct {
	Topic                *string          `toml:"topic"`
	Payload              *string          `toml:"payload"`
	ContentType          *string          `toml:"content-type"`
	FormatIndicator      *int             `toml:"format-indicator"`
	CorrelationIndex     *int             `toml:"correlation-index"`
	Qos                  *int             `toml:"qos"`
	MessageExpiry        TestCaseDuration `toml:"message-expiry"`
	Status               *string          `toml:"status"`
	StatusMessage        *string          `toml:"status-message"`
	IsApplicationError   *string          `toml:"is-application-error"`
	InvalidPropertyName  *string          `toml:"invalid-property-name"`
	InvalidPropertyValue *string          `toml:"invalid-property-value"`
}

func (receiveResponse *DefaultReceiveResponse) GetTopic() *string {
	if receiveResponse.Topic == nil {
		return nil
	}

	topic := *receiveResponse.Topic
	return &topic
}

func (receiveResponse *DefaultReceiveResponse) GetPayload() *string {
	if receiveResponse.Payload == nil {
		return nil
	}

	payload := *receiveResponse.Payload
	return &payload
}

func (receiveResponse *DefaultReceiveResponse) GetContentType() *string {
	if receiveResponse.ContentType == nil {
		return nil
	}

	contentType := *receiveResponse.ContentType
	return &contentType
}

func (receiveResponse *DefaultReceiveResponse) GetFormatIndicator() *int {
	if receiveResponse.FormatIndicator == nil {
		return nil
	}

	formatIndicator := *receiveResponse.FormatIndicator
	return &formatIndicator
}

func (receiveResponse *DefaultReceiveResponse) GetCorrelationIndex() *int {
	if receiveResponse.CorrelationIndex == nil {
		return nil
	}

	correlationIndex := *receiveResponse.CorrelationIndex
	return &correlationIndex
}

func (receiveResponse *DefaultReceiveResponse) GetQos() *int {
	if receiveResponse.Qos == nil {
		return nil
	}

	qos := *receiveResponse.Qos
	return &qos
}

func (receiveResponse *DefaultReceiveResponse) GetMessageExpiry() TestCaseDuration {
	return receiveResponse.MessageExpiry
}

func (receiveResponse *DefaultReceiveResponse) GetStatus() *string {
	if receiveResponse.Status == nil {
		return nil
	}

	status := *receiveResponse.Status
	return &status
}

func (receiveResponse *DefaultReceiveResponse) GetStatusMessage() *string {
	if receiveResponse.StatusMessage == nil {
		return nil
	}

	statusMessage := *receiveResponse.StatusMessage
	return &statusMessage
}

func (receiveResponse *DefaultReceiveResponse) GetIsApplicationError() *string {
	if receiveResponse.IsApplicationError == nil {
		return nil
	}

	isApplicationError := *receiveResponse.IsApplicationError
	return &isApplicationError
}

func (receiveResponse *DefaultReceiveResponse) GetInvalidPropertyName() *string {
	if receiveResponse.InvalidPropertyName == nil {
		return nil
	}

	invalidPropertyName := *receiveResponse.InvalidPropertyName
	return &invalidPropertyName
}

func (receiveResponse *DefaultReceiveResponse) GetInvalidPropertyValue() *string {
	if receiveResponse.InvalidPropertyValue == nil {
		return nil
	}

	invalidPropertyValue := *receiveResponse.InvalidPropertyValue
	return &invalidPropertyValue
}
