// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"errors"

	"github.com/eclipse/paho.golang/paho"
)

type publishResult struct {
	// TODO: add PUBACK information once Paho exposes it
	// (see: https://github.com/eclipse/paho.golang/issues/216)
	err error
}

type outgoingPublish struct {
	packet     *paho.Publish
	resultChan chan *publishResult
}

// Background goroutine that sends queue publishes while the connection is up.
// Blocks until ctx is cancelled.
func (c *SessionClient) manageOutgoingPublishes(ctx context.Context) {
	var nextOutgoingPublish *outgoingPublish

	for {
		pahoClient, connUp, connDown := func() (PahoClient, chan struct{}, chan struct{}) {
			c.pahoClientMu.RLock()
			defer c.pahoClientMu.RUnlock()
			return c.pahoClient, c.connUp, c.connDown
		}()

		if pahoClient == nil {
			select {
			case <-ctx.Done():
				return
			case <-connUp:
			}
			continue
		}

		waitForNextConnection := func() bool {
			for {
				select {
				case <-ctx.Done():
					return false
				case <-connDown:
					return true
				case nextOutgoingPublish = <-func() chan *outgoingPublish {
					// NOTE: This function either returns a nil channel (for
					// which a read from blocks indefinitely) or
					// c.outgoingPublishes depending on whether we are retrying
					// the PUBLISH from the previous iteration or whether we are
					// pulling in a new PUBLISH.
					if nextOutgoingPublish != nil {
						// We already have a PUBLISH we need to send, so don't
						// read the next PUBLISH from c.outgoingPublishes.
						return nil
					}
					return c.outgoingPublishes
				}():
				}

				// NOTE: we cannot get back the PUBACK on this due to a
				// limitation in Paho (see
				// https://github.com/eclipse/paho.golang/issues/216). We should
				// consider submitting a PR to Paho to address this gap.
				_, err := pahoClient.PublishWithOptions(
					ctx,
					nextOutgoingPublish.packet,
					paho.PublishOptions{Method: paho.PublishMethod_AsyncSend},
				)
				var result *publishResult
				if err == nil ||
					errors.Is(err, paho.ErrNetworkErrorAfterStored) {
					// Paho has accepted control of the PUBLISH (i.e., either
					// the PUBLISH was sent or the PUBLISH was stored in Paho's
					// session tracker), so we relinquish control of the
					// PUBLISH.
					result = &publishResult{
						// TODO: put the PUBACK in here when the Paho limitation
						// is addressed.
					}
				} else if errors.Is(err, paho.ErrInvalidArguments) {
					// Paho says the PUBLISH is invalid (likely due to an MQTT
					// spec violation). There is no hope of this PUBLISH
					// succeeding, so we will give up on this PUBLISH and notify
					// the application.
					result = &publishResult{
						err: &InvalidArgumentError{
							wrappedError: err,
							message:      "invalid arguments in Publish() options",
						},
					}
				}
				if result != nil {
					// should never block because it should be buffered by 1
					nextOutgoingPublish.resultChan <- result
					nextOutgoingPublish = nil
				}
			}
		}()
		if !waitForNextConnection {
			return
		}
	}
}

func (c *SessionClient) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...PublishOption,
) error {
	if !c.sessionStarted.Load() {
		return &ClientStateError{State: NotStarted}
	}

	var opt PublishOptions
	opt.Apply(opts)

	// Validate options.
	if opt.QoS >= 2 {
		return &InvalidArgumentError{
			message: "Invalid QoS. Supported QoS value are 0 and 1",
		}
	}
	if opt.PayloadFormat >= 2 {
		return &InvalidArgumentError{
			message: "Invalid payload format indicator. Supported values are 0 and 1",
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

	// Buffered in case the ctx is cancelled before we are able to read the
	// result
	resultChan := make(chan *publishResult, 1)
	queuedPublish := &outgoingPublish{
		packet:     pub,
		resultChan: resultChan,
	}
	select {
	case c.outgoingPublishes <- queuedPublish:
	default:
		return &PublishQueueFullError{}
	}
	var result *publishResult
	select {
	case result = <-resultChan:
	case <-ctx.Done():
		return ctx.Err()
	case <-c.shutdown:
		return &ClientStateError{State: ShutDown}
	}

	return result.err
}
