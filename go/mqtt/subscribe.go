package mqtt

import (
	"context"
	"errors"
	"fmt"
	"sync"
	"sync/atomic"

	protocolErrors "github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/eclipse/paho.golang/paho"
)

type incomingPublish struct {
	// The incoming PUBLISH packet
	packet *paho.Publish
	// The conn count on which the PUBLISH was received. This is used to discard PUBACKs if a disconnection occurs.
	connCount uint64
}

// Creates the single callback to register to the underlying Paho client for incoming PUBLISH packets
func (c *SessionClient) makeOnPublishReceived(connCount uint64) func(paho.PublishReceived) (bool, error) {
	return func(publishReceived paho.PublishReceived) (bool, error) {
		c.incomingPublishHandlerMu.Lock()
		defer c.incomingPublishHandlerMu.Unlock()

		for _, handler := range c.incomingPublishHandlers {
			handler(
				incomingPublish{
					packet:    publishReceived.Packet,
					connCount: connCount,
				},
			)
		}

		// NOTE: this return value doesn't really mean anything because this is the only OnPublishReceivedHandler on this Paho instance
		return true, nil
	}
}

// Registers a handler to a list of handlers that a called sychronously in order whenever a PUBLISH is received.
// Returns a function which removes the handler from the list when called.
func (c *SessionClient) registerIncomingPublishHandler(handler func(incomingPublish)) func() {
	c.incomingPublishHandlerMu.Lock()
	defer c.incomingPublishHandlerMu.Unlock()
	var currID uint64
ID:
	for ; ; currID++ {
		for _, existingID := range c.incomingPublishHandlerIDs {
			if currID == existingID {
				continue ID
			}
		}
		break
	}

	c.incomingPublishHandlers = append(c.incomingPublishHandlers, handler)
	c.incomingPublishHandlerIDs = append(c.incomingPublishHandlerIDs, currID)

	return func() {
		c.incomingPublishHandlerMu.Lock()
		defer c.incomingPublishHandlerMu.Unlock()
		for i, existingID := range c.incomingPublishHandlerIDs {
			if currID == existingID {
				c.incomingPublishHandlers = append(c.incomingPublishHandlers[:i], c.incomingPublishHandlers[i+1:]...)
				c.incomingPublishHandlerIDs = append(c.incomingPublishHandlerIDs[:i], c.incomingPublishHandlerIDs[i+1:]...)
				return
			}
		}
	}
}

func (c *SessionClient) Subscribe(
	ctx context.Context,
	topic string,
	handler mqtt.MessageHandler,
	opts ...mqtt.SubscribeOption,
) (mqtt.Subscription, error) {
	sub, err := buildSubscribe(topic, opts...)
	if err != nil {
		return nil, err
	}

	removeHandlerFunc := c.registerIncomingPublishHandler(func(incoming incomingPublish) {
		// TODO
	})

	for {
		c.pahoClientMu.RLock()
		pahoClient := c.pahoClient
		connUp := c.connUp
		connDown := c.connDown
		c.pahoClientMu.RUnlock()

		if pahoClient == nil {
			select {
			case <-c.shutdown:
				removeHandlerFunc()
				return nil, fmt.Errorf("session client is shutting down")
			case <-ctx.Done():
				removeHandlerFunc()
				return nil, fmt.Errorf("context cancelled: %w", ctx.Err())
			case <-connUp:
			}
			continue
		}
		// TODO: figure out what to do with suback
		suback, err := pahoClient.Subscribe(ctx, sub)
		if errors.Is(err, paho.ErrInvalidArguments) {
			removeHandlerFunc()
			return nil, fmt.Errorf("invalid arguments in subscribe options: %w", err)
		}
		if suback != nil {
			return &subscription{
				SessionClient:     c,
				topic:             topic,
				removeHandlerFunc: sync.OnceFunc(removeHandlerFunc),
			}, nil
		}

		// If we get here, the SUBSCRIBE failed because the connection is down or because ctx was cancelled.
		select {
		case <-ctx.Done():
			removeHandlerFunc()
			fmt.Errorf("context cancelled: %w", ctx.Err())
		case <-c.shutdown:
			removeHandlerFunc()
			fmt.Errorf("session client is shutting down")
		case <-connDown:
			// Connection is down, wait for the connection to come back up and retry
		}
	}
}

type subscription struct {
	*SessionClient
	topic             string
	removeHandlerFunc func()
}

func (s *subscription) Unsubscribe(
	ctx context.Context,
	opts ...mqtt.UnsubscribeOption,
) error {
	c := s.SessionClient

	unsub, err := buildUnsubscribe(s.topic, opts...)
	if err != nil {
		return err
	}

	for {
		c.pahoClientMu.RLock()
		pahoClient := c.pahoClient
		connUp := c.connUp
		connDown := c.connDown
		c.pahoClientMu.RUnlock()

		if pahoClient == nil {
			select {
			case <-c.shutdown:
				return fmt.Errorf("session client is shutting down")
			case <-ctx.Done():
				return fmt.Errorf("context cancelled: %w", ctx.Err())
			case <-connUp:
			}
			continue
		}
		// TODO: figure out what to do with unsuback
		unsuback, err := pahoClient.Unsubscribe(ctx, unsub)
		if errors.Is(err, paho.ErrInvalidArguments) {
			return fmt.Errorf("invalid arguments in unsubscribe options: %w", err)
		}
		if unsuback != nil {
			s.removeHandlerFunc()
			return nil
		}

		// If we get here, the UNSUBSCRIBE failed because the connection is down or because ctx was cancelled.
		select {
		case <-ctx.Done():
			fmt.Errorf("context cancelled: %w", ctx.Err())
		case <-c.shutdown:
			fmt.Errorf("session client is shutting down")
		case <-connDown:
			// Connection is down, wait for the connection to come back up and retry
		}
	}
}

func buildSubscribe(
	topic string,
	opts ...mqtt.SubscribeOption,
) (*paho.Subscribe, error) {
	var opt mqtt.SubscribeOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return nil, &protocolErrors.Error{
			Kind:          protocolErrors.ConfigurationInvalid,
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
	connCount := atomic.LoadUint64(&c.connCount)
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
				return &protocolErrors.Error{
					Kind:    protocolErrors.ExecutionException,
					Message: "cannot ack a QoS 0 message",
				}
			}

			if connCount != atomic.LoadUint64(&c.connCount) {
				return &protocolErrors.Error{
					Kind:    protocolErrors.ExecutionException,
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
