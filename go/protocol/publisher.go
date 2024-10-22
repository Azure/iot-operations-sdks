// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
	"github.com/google/uuid"
)

// Provide the shared implementation details for the MQTT publishers.
type publisher[T any] struct {
	encoding Encoding[T]
	topic    *internal.TopicPattern
}

// DefaultTimeout is the timeout applied to Invoke or Send if none is specified.
const DefaultTimeout = 10 * time.Second

func (p *publisher[T]) build(
	msg *Message[T],
	topicTokens map[string]string,
	timeout *internal.Timeout,
) (*mqtt.Message, error) {
	pub := &mqtt.Message{}
	var err error

	if p.topic != nil {
		pub.Topic, err = p.topic.Topic(topicTokens)
		if err != nil {
			return nil, err
		}
	}

	pub.PublishOptions = mqtt.PublishOptions{
		QoS:           1,
		MessageExpiry: timeout.MessageExpiry(),
	}

	if msg != nil {
		pub.Payload, err = serialize(p.encoding, msg.Payload)
		if err != nil {
			return nil, err
		}

		pub.ContentType = p.encoding.ContentType()
		pub.PayloadFormat = p.encoding.PayloadFormat()

		if msg.CorrelationData != "" {
			correlationData, err := uuid.Parse(msg.CorrelationData)
			if err != nil {
				return nil, &errors.Error{
					Message: "correlation data is not a valid UUID",
					Kind:    errors.InternalLogicError,
				}
			}
			pub.CorrelationData = correlationData[:]
		}

		pub.UserProperties, err = internal.MetadataToProp(msg.Metadata)
		if err != nil {
			return nil, err
		}
	} else {
		pub.UserProperties = map[string]string{}
	}

	ts, err := hlc.Get()
	if err != nil {
		return nil, err
	}
	pub.UserProperties[constants.Timestamp] = ts.String()
	pub.UserProperties[constants.ProtocolVersion] = version.ProtocolString

	return pub, nil
}
