// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/eclipse/paho.golang/paho"
)

func (c *SessionClient) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...PublishOption,
) (*Ack, error) {
	if err := c.prepare(ctx); err != nil {
		return nil, err
	}

	var opt PublishOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return nil, &errors.Error{
			Kind:          errors.ArgumentInvalid,
			Message:       "unsupported QoS",
			PropertyName:  "QoS",
			PropertyValue: opt.QoS,
		}
	}
	if opt.PayloadFormat >= 2 {
		return nil, &errors.Error{
			Kind:          errors.ArgumentInvalid,
			Message:       "invalid payload format",
			PropertyName:  "PayloadFormat",
			PropertyValue: opt.PayloadFormat,
		}
	}

	var zeroValueAck *Ack
	if opt.QoS == 1 {
		zeroValueAck = &Ack{}
	}

	// Build MQTT publish packet.
	pub := &paho.Publish{
		QoS:     opt.QoS,
		Retain:  opt.Retain,
		Topic:   topic,
		Payload: payload,
		Properties: &paho.PublishProperties{
			ContentType:     opt.ContentType,
			CorrelationData: opt.CorrelationData,
			PayloadFormat:   &opt.PayloadFormat,
			ResponseTopic:   opt.ResponseTopic,
			User:            mapToUserProperties(opt.UserProperties),
		},
	}

	if opt.MessageExpiry > 0 {
		pub.Properties.MessageExpiry = &opt.MessageExpiry
	}

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		err := c.bufferPacket(
			ctx,
			&queuedPacket{packet: pub},
		)
		if err != nil {
			return nil, err
		}
		return zeroValueAck, nil
	}

	// Execute the publish.
	c.log.Packet(ctx, "publish", pub)
	err := pahoPub(ctx, c.pahoClient, pub)
	if err != nil {
		return nil, err
	}
	return zeroValueAck, nil
}
