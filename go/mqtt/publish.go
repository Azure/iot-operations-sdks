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
) error {
	if err := c.prepare(ctx); err != nil {
		return err
	}

	var opt PublishOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return &errors.Error{
			Kind:          errors.ArgumentInvalid,
			Message:       "unsupported QoS",
			PropertyName:  "QoS",
			PropertyValue: opt.QoS,
		}
	}
	if opt.PayloadFormat >= 2 {
		return &errors.Error{
			Kind:          errors.ArgumentInvalid,
			Message:       "invalid payload format",
			PropertyName:  "PayloadFormat",
			PropertyValue: opt.PayloadFormat,
		}
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
		return c.bufferPacket(
			ctx,
			&queuedPacket{packet: pub},
		)
	}

	// Execute the publish.
	c.logPublish(pub)
	return pahoPub(ctx, c.pahoClient, pub)
}
