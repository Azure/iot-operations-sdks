// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"sync"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/eclipse/paho.golang/paho"
)

// RegisterMessageHandler registers a message handler on this client. Returns a
// callback to remove the message handler.
func (c *SessionClient) RegisterMessageHandler(handler MessageHandler) func() {
	ctx, cancel := context.WithCancel(context.Background())
	done := c.incomingPublishHandlers.AppendEntry(
		func(incoming *paho.Publish) bool {
			return handler(ctx, c.buildMessage(incoming))
		},
	)
	return sync.OnceFunc(func() {
		done()
		cancel()
	})
}

func (c *SessionClient) Subscribe(
	ctx context.Context,
	topic string,
	opts ...SubscribeOption,
) (*Ack, error) {
	if err := c.prepare(ctx); err != nil {
		return nil, err
	}

	sub, err := buildSubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		err := c.bufferPacket(ctx, &queuedPacket{packet: sub})
		if err != nil {
			return nil, err
		}
		return &Ack{}, nil
	}

	// Execute the subscribe.
	c.log.Packet(ctx, "subscribe", sub)
	err = pahoSub(ctx, c.pahoClient, sub)
	if err != nil {
		return nil, err
	}
	return &Ack{}, nil
}

func (c *SessionClient) onPublishReceived(
	pb paho.PublishReceived,
) (bool, error) {
	var willAck bool
	for handler := range c.incomingPublishHandlers.All() {
		willAck = handler(pb.Packet) || willAck
	}
	if !willAck {
		return true, pahoAck(c.pahoClient, pb.Packet)
	}
	return true, nil
}

// Unsubscribe from a topic.
func (c *SessionClient) Unsubscribe(
	ctx context.Context,
	topic string,
	opts ...UnsubscribeOption,
) (*Ack, error) {
	if err := c.prepare(ctx); err != nil {
		return nil, err
	}

	unsub, err := buildUnsubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		err := c.bufferPacket(ctx, &queuedPacket{packet: unsub})
		if err != nil {
			return nil, err
		}
		return &Ack{}, err
	}

	c.log.Packet(ctx, "unsubscribe", unsub)
	err = pahoUnsub(ctx, c.pahoClient, unsub)
	if err != nil {
		return nil, err
	}
	return &Ack{}, err
}

func buildSubscribe(
	topic string,
	opts ...SubscribeOption,
) (*paho.Subscribe, error) {
	var opt SubscribeOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return nil, &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "unsupported QoS",
			PropertyName:  "QoS",
			PropertyValue: opt.QoS,
		}
	}

	// Build MQTT subscribe packet.
	sub := &paho.Subscribe{
		Subscriptions: []paho.SubscribeOptions{{
			Topic:             topic,
			QoS:               opt.QoS,
			NoLocal:           opt.NoLocal,
			RetainAsPublished: opt.Retain,
			RetainHandling:    opt.RetainHandling,
		}},
	}
	if len(opt.UserProperties) > 0 {
		sub.Properties = &paho.SubscribeProperties{
			User: mapToUserProperties(opt.UserProperties),
		}
	}
	return sub, nil
}

func buildUnsubscribe(
	topic string,
	opts ...UnsubscribeOption,
) (*paho.Unsubscribe, error) {
	var opt UnsubscribeOptions
	opt.Apply(opts)

	unsub := &paho.Unsubscribe{
		Topics: []string{topic},
	}
	if len(opt.UserProperties) > 0 {
		unsub.Properties = &paho.UnsubscribeProperties{
			User: mapToUserProperties(opt.UserProperties),
		}
	}

	return unsub, nil
}

// buildMessage build message for message handler.
func (c *SessionClient) buildMessage(p *paho.Publish) *Message {
	// TODO: MQTT server is allowed to send multiple copies if there are
	// multiple topic filter matches a message, thus if we see same message
	// multiple times, we need to check their QoS before send the Ack().
	var acked bool
	connCount := atomic.LoadInt64(&c.connCount)
	msg := &Message{
		Topic:   p.Topic,
		Payload: p.Payload,
		PublishOptions: PublishOptions{
			ContentType:     p.Properties.ContentType,
			CorrelationData: p.Properties.CorrelationData,
			QoS:             p.QoS,
			ResponseTopic:   p.Properties.ResponseTopic,
			Retain:          p.Retain,
			UserProperties:  userPropertiesToMap(p.Properties.User),
		},
		Ack: func() error {
			// More than one ack is a no-op.
			if acked {
				return nil
			}

			if p.QoS == 0 {
				return &errors.Error{
					Kind:    errors.ExecutionException,
					Message: "cannot ack a QoS 0 message",
				}
			}

			if connCount != atomic.LoadInt64(&c.connCount) {
				return &errors.Error{
					Kind:    errors.ExecutionException,
					Message: "connection lost before ack",
				}
			}

			if err := pahoAck(c.pahoClient, p); err != nil {
				return err
			}

			acked = true
			return nil
		},
	}
	if p.Properties.MessageExpiry != nil {
		msg.MessageExpiry = *p.Properties.MessageExpiry
	}
	if p.Properties.PayloadFormat != nil {
		msg.PayloadFormat = *p.Properties.PayloadFormat
	}
	return msg
}
