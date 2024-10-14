// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"errors"
	"sync"

	"github.com/eclipse/paho.golang/paho"
)

type incomingPublish struct {
	// The incoming PUBLISH packet
	packet *paho.Publish
	// Manually acks this PUBLISH. Note that automatic acks are not currently
	// supported, so this MUST be called.
	ack func() error
}

// Creates the single callback to register to the underlying Paho client for
// incoming PUBLISH packets
func (c *SessionClient) makeOnPublishReceived(connCount uint64) func(paho.PublishReceived) (bool, error) {
	return func(publishReceived paho.PublishReceived) (bool, error) {
		var ackOnce sync.Once

		ack := func() error {
			if publishReceived.Packet.QoS == 0 {
				return &InvalidOperationError{
					message: "only QoS 1 messages may be acked",
				}
			}

			ackOnce.Do(func() {
				pahoClient, currConnCount := func() (PahoClient, uint64) {
					c.pahoClientMu.RLock()
					defer c.pahoClientMu.RUnlock()
					return c.pahoClient, c.connCount
				}()

				if pahoClient == nil || connCount != currConnCount {
					// if any disconnections occurred since receiving this
					// PUBLISH, discard the ack.
					return
				}
				pahoClient.Ack(publishReceived.Packet)
			})
			return nil
		}

		for handler := range c.incomingPublishHandlers.All() {
			handler(
				incomingPublish{
					packet: publishReceived.Packet,
					ack:    ack,
				},
			)
		}

		// NOTE: this return value doesn't really mean anything because this is
		// the only OnPublishReceivedHandler on this Paho instance
		return true, nil
	}
}

// RegisterMessageHandler registers a message handler on this client. Returns a
// callback to remove the message handler.
func (c *SessionClient) RegisterMessageHandler(handler MessageHandler) func() {
	ctx, cancel := context.WithCancel(context.Background())
	done := c.incomingPublishHandlers.AppendEntry(
		func(incoming incomingPublish) {
			handler(ctx, c.buildMessage(incoming))
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
) error {
	if !c.sessionStarted.Load() {
		return &ClientStateError{NotStarted}
	}
	sub, err := buildSubscribe(topic, opts...)
	if err != nil {
		return err
	}

	for {
		pahoClient, connUp, connDown := func() (PahoClient, chan struct{}, chan struct{}) {
			c.pahoClientMu.RLock()
			defer c.pahoClientMu.RUnlock()
			return c.pahoClient, c.connUp, c.connDown
		}()

		if pahoClient == nil {
			select {
			case <-c.shutdown:
				return &ClientStateError{State: ShutDown}
			case <-ctx.Done():
				return ctx.Err()
			case <-connUp:
			}
			continue
		}

		suback, err := pahoClient.Subscribe(ctx, sub)
		if errors.Is(err, paho.ErrInvalidArguments) {
			return &InvalidArgumentError{
				wrappedError: err,
				message:      "invalid arguments in Subscribe() options",
			}
		}
		if suback != nil {
			return nil
		}

		// If we get here, the SUBSCRIBE failed because the connection is down
		// or because ctx was cancelled.
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-c.shutdown:
			return &ClientStateError{State: ShutDown}
		case <-connDown:
			// Connection is down, wait for the connection to come back up and
			// retry
		}
	}
}

func (c *SessionClient) Unsubscribe(
	ctx context.Context,
	topic string,
	opts ...UnsubscribeOption,
) error {
	unsub, err := buildUnsubscribe(topic, opts...)
	if err != nil {
		return err
	}

	for {
		pahoClient, connUp, connDown := func() (PahoClient, chan struct{}, chan struct{}) {
			c.pahoClientMu.RLock()
			defer c.pahoClientMu.RUnlock()
			return c.pahoClient, c.connUp, c.connDown
		}()

		if pahoClient == nil {
			select {
			case <-c.shutdown:
				return &ClientStateError{State: ShutDown}
			case <-ctx.Done():
				return ctx.Err()
			case <-connUp:
			}
			continue
		}

		unsuback, err := pahoClient.Unsubscribe(ctx, unsub)
		if errors.Is(err, paho.ErrInvalidArguments) {
			return &InvalidArgumentError{
				wrappedError: err,
				message:      "invalid arguments in Unsubscribe() options",
			}
		}
		if unsuback != nil {
			return nil
		}

		// If we get here, the UNSUBSCRIBE failed because the connection is down
		// or because ctx was cancelled.
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-c.shutdown:
			return &ClientStateError{State: ShutDown}
		case <-connDown:
			// Connection is down, wait for the connection to come back up and
			// retry
		}
	}
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
			message: "Invalid QoS. Supported QoS value are 0 and 1",
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
func (c *SessionClient) buildMessage(p incomingPublish) *Message {
	msg := &Message{
		Topic:   p.packet.Topic,
		Payload: p.packet.Payload,
		PublishOptions: PublishOptions{
			ContentType:     p.packet.Properties.ContentType,
			CorrelationData: p.packet.Properties.CorrelationData,
			QoS:             p.packet.QoS,
			ResponseTopic:   p.packet.Properties.ResponseTopic,
			Retain:          p.packet.Retain,
			UserProperties:  userPropertiesToMap(p.packet.Properties.User),
		},
		Ack: p.ack,
	}
	if p.packet.Properties.MessageExpiry != nil {
		msg.MessageExpiry = *p.packet.Properties.MessageExpiry
	}
	if p.packet.Properties.PayloadFormat != nil {
		msg.PayloadFormat = *p.packet.Properties.PayloadFormat
	}
	return msg
}
