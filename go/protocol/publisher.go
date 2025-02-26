// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
	"github.com/google/uuid"
)

// Provide the shared implementation details for the MQTT publishers.
type publisher[T any] struct {
	app      *Application
	client   MqttClient
	encoding Encoding[T]
	topic    *internal.TopicPattern
	log      log.Logger
	version  string
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
		data, err := serialize(p.encoding, msg.Payload)
		if err != nil {
			return nil, err
		}

		pub.Payload = data.Payload
		pub.ContentType = data.ContentType
		pub.PayloadFormat = data.PayloadFormat

		if msg.CorrelationData != "" {
			correlationData, err := uuid.Parse(msg.CorrelationData)
			if err != nil {
				return nil, &errors.Remote{
					Base: errors.Base{
						Message: "correlation data is not a valid UUID",
						Kind:    errors.InternalLogicError,
					},
				}
			}
			pub.CorrelationData = correlationData[:]
		}

		if msg.Metadata != nil {
			pub.UserProperties = msg.Metadata
		} else {
			pub.UserProperties = map[string]string{}
		}
	} else {
		pub.UserProperties = map[string]string{}
	}

	ts, err := p.app.hlc.Get()
	if err != nil {
		return nil, err
	}
	pub.UserProperties[constants.SourceID] = p.client.ID()
	pub.UserProperties[constants.Timestamp] = ts.String()
	pub.UserProperties[constants.ProtocolVersion] = p.version

	return pub, nil
}

func (p *publisher[T]) publish(ctx context.Context, msg *mqtt.Message) error {
	ack, err := p.client.Publish(
		ctx,
		msg.Topic,
		msg.Payload,
		&msg.PublishOptions,
	)
	return errutil.Mqtt(ctx, "publish", ack, err)
}
