// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"errors"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/eclipse/paho.golang/paho"
)

type messageHandler func(*Message) bool

// Creates the single callback to register to the underlying Paho client for
// incoming PUBLISH packets.
func (c *SessionClient) makeOnPublishReceived(
	ctx context.Context,
	attempt uint64,
) func(paho.PublishReceived) (bool, error) {
	return func(publishReceived paho.PublishReceived) (bool, error) {
		packet := publishReceived.Packet
		c.log.Packet(ctx, "publish received", packet)

		// We track whether any of the handlers take ownership of the message
		// so that we only actually ack once all of them have done so.
		var willAck sync.WaitGroup
		for handler := range c.messageHandlers.All() {
			willAck.Add(1)
			msg := buildMessage(packet, sync.OnceValue(func() error {
				if packet.QoS == 0 {
					return &InvalidOperationError{
						message: "QoS 0 messages may not be acked",
					}
				}
				willAck.Done()
				return nil
			}))
			if !handler(msg) && packet.QoS > 0 {
				// Use the passed-in ack to trigger the sync.OnceValue, which
				// ensures misbehaving handlers can't accidentally double-ack.
				_ = msg.Ack()
			}
		}

		if packet.QoS > 0 {
			go func() {
				willAck.Wait()
				current := c.conn.Current()

				// If any disconnections occurred since receiving this PUBLISH,
				// discard the ack.
				if current.Client == nil || current.Attempt != attempt {
					return
				}

				// Errors from Ack are highly unlikely, so just log them.
				if err := current.Client.Ack(packet); err != nil {
					c.log.Error(ctx, err)
				}
			}()
		}
		return true, nil
	}
}

// RegisterMessageHandler registers a message handler on this client. Returns a
// callback to remove the message handler.
func (c *SessionClient) RegisterMessageHandler(handler MessageHandler) func() {
	ctx, cancel := context.WithCancel(context.Background())
	done := c.messageHandlers.AppendEntry(func(msg *Message) bool {
		return handler(ctx, msg)
	})
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
	if !c.sessionStarted.Load() {
		return nil, &ClientStateError{NotStarted}
	}
	sub, err := buildSubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	ctx, cancel := c.shutdown.With(ctx)
	defer cancel()

	for ctx, pahoClient := range c.conn.Client(ctx) {
		c.log.Packet(ctx, "subscribe", sub)
		suback, err := pahoClient.Subscribe(ctx, sub)
		c.log.Packet(ctx, "suback", suback)

		if errors.Is(err, paho.ErrInvalidArguments) {
			return nil, &InvalidArgumentError{
				wrapped: err,
				message: "invalid arguments in Subscribe() options",
			}
		}

		if suback != nil {
			return &Ack{
				ReasonCode:   suback.Reasons[0],
				ReasonString: suback.Properties.ReasonString,
				UserProperties: internal.UserPropertiesToMap(
					suback.Properties.User,
				),
			}, nil
		}
	}

	return nil, context.Cause(ctx)
}

func (c *SessionClient) Unsubscribe(
	ctx context.Context,
	topic string,
	opts ...UnsubscribeOption,
) (*Ack, error) {
	if !c.sessionStarted.Load() {
		return nil, &ClientStateError{NotStarted}
	}
	unsub, err := buildUnsubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	ctx, cancel := c.shutdown.With(ctx)
	defer cancel()

	for ctx, pahoClient := range c.conn.Client(ctx) {
		c.log.Packet(ctx, "unsubscribe", unsub)
		unsuback, err := pahoClient.Unsubscribe(ctx, unsub)
		c.log.Packet(ctx, "unsuback", unsuback)

		if errors.Is(err, paho.ErrInvalidArguments) {
			return nil, &InvalidArgumentError{
				wrapped: err,
				message: "invalid arguments in Unsubscribe() options",
			}
		}

		if unsuback != nil {
			return &Ack{
				ReasonCode:   unsuback.Reasons[0],
				ReasonString: unsuback.Properties.ReasonString,
				UserProperties: internal.UserPropertiesToMap(
					unsuback.Properties.User,
				),
			}, nil
		}
	}

	return nil, context.Cause(ctx)
}

func buildSubscribe(
	topic string,
	opts ...SubscribeOption,
) (*paho.Subscribe, error) {
	var opt SubscribeOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return nil, &InvalidArgumentError{
			message: "invalid or unsupported QoS",
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
			User: internal.MapToUserProperties(opt.UserProperties),
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
			User: internal.MapToUserProperties(opt.UserProperties),
		}
	}

	return unsub, nil
}

// Build message for the message handler.
func buildMessage(packet *paho.Publish, ack func() error) *Message {
	msg := &Message{
		Topic:   packet.Topic,
		Payload: packet.Payload,
		PublishOptions: PublishOptions{
			ContentType:     packet.Properties.ContentType,
			CorrelationData: packet.Properties.CorrelationData,
			QoS:             packet.QoS,
			ResponseTopic:   packet.Properties.ResponseTopic,
			Retain:          packet.Retain,
			UserProperties: internal.UserPropertiesToMap(
				packet.Properties.User,
			),
		},
		Ack: ack,
	}
	if packet.Properties.MessageExpiry != nil {
		msg.MessageExpiry = *packet.Properties.MessageExpiry
	}
	if packet.Properties.PayloadFormat != nil {
		msg.PayloadFormat = *packet.Properties.PayloadFormat
	}
	return msg
}
