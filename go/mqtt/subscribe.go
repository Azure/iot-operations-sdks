package mqtt

import (
	"context"
	"fmt"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/eclipse/paho.golang/paho"
)

func (c *SessionClient) Subscribe(
	ctx context.Context,
	topic string,
	handler mqtt.MessageHandler,
	opts ...mqtt.SubscribeOption,
) (mqtt.Subscription, error) {
	if err := c.prepare(ctx); err != nil {
		return nil, err
	}

	// Subscribe, unsubscribe, and update subscription options
	// cannot be run simultaneously.
	c.subscriptionsMu.Lock()
	defer c.subscriptionsMu.Unlock()

	if _, ok := c.subscriptions[topic]; ok {
		return nil, &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "cannot subscribe to existing topic",
			PropertyName:  "topic",
			PropertyValue: topic,
		}
	}

	sub, err := buildSubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	s := &subscription{c, topic, handler}
	c.subscriptions[topic] = s

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		if err := c.bufferPacket(
			ctx,
			&queuedPacket{packet: sub, subscription: s},
		); err != nil {
			return nil, err
		}
		return s, nil
	}

	// Execute the subscribe.
	c.logSubscribe(sub)
	if err := pahoSub(ctx, c.pahoClient, sub); err != nil {
		delete(c.subscriptions, topic)
		return nil, err
	}

	return s, nil
}

func (c *SessionClient) Register(
	topic string,
	handler mqtt.MessageHandler,
) (mqtt.Subscription, error) {
	// Subscribe, unsubscribe, and update subscription options
	// cannot be run simultaneously.
	c.subscriptionsMu.Lock()
	defer c.subscriptionsMu.Unlock()

	if _, ok := c.subscriptions[topic]; ok {
		return nil, &errors.Error{
			Kind:          errors.ConfigurationInvalid,
			Message:       "cannot subscribe to existing topic",
			PropertyName:  "topic",
			PropertyValue: topic,
		}
	}

	s := &subscription{c, topic, handler}
	c.subscriptions[topic] = s

	return s, nil
}

func (c *SessionClient) onPublishReceived(
	ctx context.Context,
) []func(paho.PublishReceived) (bool, error) {
	return []func(paho.PublishReceived) (bool, error){
		func(pb paho.PublishReceived) (bool, error) {
			c.subscriptionsMu.RLock()
			defer c.subscriptionsMu.RUnlock()

			for _, s := range c.subscriptions {
				if isTopicFilterMatch(s.topic, pb.Packet.Topic) {
					msg := s.buildMessage(pb.Packet)
					if err := s.handler(ctx, msg); err != nil {
						s.error(fmt.Sprintf(
							"failed to execute the handler on message: %s",
							err.Error(),
						))
						return false, err
					}
					return true, nil
				}
			}

			return false, nil
		},
	}
}

// Helper function for user to update subscribe options.
func (s *subscription) Update(
	ctx context.Context,
	opts ...mqtt.SubscribeOption,
) error {
	c := s.SessionClient

	if err := c.prepare(ctx); err != nil {
		return err
	}

	// Subscribe, unsubscribe, and update subscription options
	// cannot be run simultaneously.
	c.subscriptionsMu.Lock()
	defer c.subscriptionsMu.Unlock()

	if _, ok := s.subscriptions[s.topic]; !ok {
		return &errors.Error{
			Kind:          errors.StateInvalid,
			Message:       "cannot update unsubscribed topic",
			PropertyName:  "topic",
			PropertyValue: s.topic,
		}
	}

	sub, err := buildSubscribe(s.topic, opts...)
	if err != nil {
		return err
	}

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		return c.bufferPacket(
			ctx,
			&queuedPacket{packet: sub},
		)
	}

	c.logPacket(sub)
	return pahoSub(ctx, c.pahoClient, sub)
}

// Helper function for user to unsubscribe topic.
func (s *subscription) Unsubscribe(
	ctx context.Context,
	opts ...mqtt.UnsubscribeOption,
) error {
	c := s.SessionClient

	if err := c.prepare(ctx); err != nil {
		return err
	}

	// Subscribe, unsubscribe, and update subscription options
	// cannot be run simultaneously.
	c.subscriptionsMu.Lock()
	defer c.subscriptionsMu.Unlock()

	unsub, err := buildUnsubscribe(s.topic, opts...)
	if err != nil {
		return err
	}

	// Connection lost; buffer the packet for reconnection.
	if !c.isConnected.Load() {
		return c.bufferPacket(
			ctx,
			&queuedPacket{packet: unsub, subscription: s},
		)
	}

	c.logPacket(unsub)
	if err := pahoUnsub(ctx, c.pahoClient, unsub); err != nil {
		return err
	}

	// Remove subscribed topic and callback.
	delete(s.subscriptions, s.topic)

	return nil
}

func buildSubscribe(
	topic string,
	opts ...mqtt.SubscribeOption,
) (*paho.Subscribe, error) {
	var opt mqtt.SubscribeOptions
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
			QoS:               byte(opt.QoS),
			NoLocal:           opt.NoLocal,
			RetainAsPublished: opt.Retain,
			RetainHandling:    byte(opt.RetainHandling),
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
	opts ...mqtt.UnsubscribeOption,
) (*paho.Unsubscribe, error) {
	var opt mqtt.UnsubscribeOptions
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
func (c *SessionClient) buildMessage(p *paho.Publish) *mqtt.Message {
	// TODO: MQTT server is allowed to send multiple copies if there are
	// multiple topic filter matches a message, thus if we see same message
	// multiple times, we need to check their QoS before send the Ack().
	var acked bool
	connCount := atomic.LoadInt64(&c.connCount)
	msg := &mqtt.Message{
		Topic:   p.Topic,
		Payload: p.Payload,
		PublishOptions: mqtt.PublishOptions{
			ContentType:     p.Properties.ContentType,
			CorrelationData: p.Properties.CorrelationData,
			QoS:             mqtt.QoS(p.QoS),
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

			c.logAck(p)
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
		msg.PayloadFormat = mqtt.PayloadFormat(*p.Properties.PayloadFormat)
	}
	return msg
}
