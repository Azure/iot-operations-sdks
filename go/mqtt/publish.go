package mqtt

import (
	"context"

	"github.com/eclipse/paho.golang/paho"
	"github.com/microsoft/mqtt-patterns/lib/go/protocol/errors"
	"github.com/microsoft/mqtt-patterns/lib/go/protocol/mqtt"
)

func (c *SessionClient) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...mqtt.PublishOption,
) error {
	if err := c.prepare(ctx); err != nil {
		return err
	}

	var opt mqtt.PublishOptions
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

	payloadFormat := byte(opt.PayloadFormat)

	// Build MQTT publish packet.
	pub := &paho.Publish{
		QoS:     byte(opt.QoS),
		Retain:  opt.Retain,
		Topic:   topic,
		Payload: payload,
		Properties: &paho.PublishProperties{
			ContentType:     opt.ContentType,
			CorrelationData: opt.CorrelationData,
			PayloadFormat:   &payloadFormat,
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
