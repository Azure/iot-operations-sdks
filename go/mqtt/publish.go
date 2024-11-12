// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"errors"
	"iter"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/eclipse/paho.golang/paho"
)

type (
	publishResult struct {
		ack *paho.PublishResponse
		err error
	}

	outgoingPublish struct {
		packet *paho.Publish
		result chan *publishResult
	}
)

// Background goroutine that sends queue publishes while the connection is up.
// Blocks until ctx is cancelled.
func (c *SessionClient) manageOutgoingPublishes(ctx context.Context) {
	var pub *outgoingPublish
	for ctx, pahoClient := range c.conn.Client(ctx) {
		// If we have a pending publish, try to send it now.
		if pub != nil && !c.sendOutgoingPublish(ctx, pahoClient, pub) {
			continue
		}

		// Get outgoing publishes. If one fails, break the loop before nilling
		// it out in order to retry it.
		for pub = range c.nextOutgoingPublish(ctx) {
			if !c.sendOutgoingPublish(ctx, pahoClient, pub) {
				break
			}
			pub = nil
		}
	}
}

// Get the next outgoing publish until the connection or context drops.
func (c *SessionClient) nextOutgoingPublish(
	ctx context.Context,
) iter.Seq[*outgoingPublish] {
	return func(yield func(*outgoingPublish) bool) {
		for {
			select {
			case <-ctx.Done():
				return
			case pub := <-c.outgoingPublishes:
				if !yield(pub) {
					return
				}
			}
		}
	}
}

// Attempt to send an outgoing publish packet and return the result to its
// channel. Returns whether it was successful.
func (c *SessionClient) sendOutgoingPublish(
	ctx context.Context,
	pahoClient *paho.Client,
	pub *outgoingPublish,
) bool {
	// NOTE: we cannot get back the PUBACK on this due to a limitation in Paho
	// (see https://github.com/eclipse/paho.golang/issues/216). We should
	// consider submitting a PR to Paho to address this gap.
	c.log.Packet(ctx, "publish", pub.packet)
	_, err := pahoClient.PublishWithOptions(
		ctx,
		pub.packet,
		paho.PublishOptions{Method: paho.PublishMethod_AsyncSend},
	)
	c.log.Packet(ctx, "puback", nil)

	if err == nil || errors.Is(err, paho.ErrNetworkErrorAfterStored) {
		// Paho has accepted control of the PUBLISH (i.e., either the PUBLISH
		// was sent or the PUBLISH was stored in Paho's session tracker), so we
		// relinquish control of the PUBLISH.
		pub.result <- &publishResult{
			// TODO: Add real PUBACK information once Paho exposes it.
			// (see: https://github.com/eclipse/paho.golang/issues/216)
			ack: &paho.PublishResponse{
				Properties: &paho.PublishResponseProperties{},
			},
		}
		return true
	}

	if errors.Is(err, paho.ErrInvalidArguments) {
		// Paho says the PUBLISH is invalid (likely due to an MQTT spec
		// violation). There is no hope of this PUBLISH succeeding, so we will
		// give up on this PUBLISH and notify the application.
		pub.result <- &publishResult{
			err: &InvalidArgumentError{
				message: "invalid arguments in PUBLISH options",
				wrapped: err,
			},
		}
		return true
	}

	return false
}

func (c *SessionClient) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...PublishOption,
) (*Ack, error) {
	if !c.sessionStarted.Load() {
		return nil, &ClientStateError{State: NotStarted}
	}

	pub, err := buildPublish(topic, payload, opts...)
	if err != nil {
		return nil, err
	}

	ctx, cancel := c.shutdown.With(ctx)
	defer cancel()

	// Buffered in case the ctx is cancelled before we are able to read the
	// result.
	queuedPublish := &outgoingPublish{pub, make(chan *publishResult, 1)}
	select {
	case c.outgoingPublishes <- queuedPublish:
	default:
		return nil, &PublishQueueFullError{}
	}

	select {
	case result := <-queuedPublish.result:
		if result.ack != nil {
			return &Ack{
				ReasonCode:   result.ack.ReasonCode,
				ReasonString: result.ack.Properties.ReasonString,
				UserProperties: internal.UserPropertiesToMap(
					result.ack.Properties.User,
				),
			}, nil
		}
		return nil, result.err

	case <-ctx.Done():
		return nil, context.Cause(ctx)
	}
}

func buildPublish(
	topic string,
	payload []byte,
	opts ...PublishOption,
) (*paho.Publish, error) {
	var opt PublishOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return nil, &InvalidArgumentError{
			message: "invalid or unsupported QoS",
		}
	}
	if opt.PayloadFormat >= 2 {
		return nil, &InvalidArgumentError{
			message: "invalid payload format indicator",
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
			User:            internal.MapToUserProperties(opt.UserProperties),
		},
	}

	if opt.MessageExpiry > 0 {
		pub.Properties.MessageExpiry = &opt.MessageExpiry
	}

	return pub, nil
}
