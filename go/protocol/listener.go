// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"fmt"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
	"github.com/google/uuid"
)

type (
	// Listener represents an object which will listen to a MQTT topic.
	Listener interface {
		Start(context.Context) error
		Close()
	}

	// Listeners represents a collection of MQTT listeners.
	Listeners []Listener

	// Provide the shared implementation details for the MQTT listeners.
	listener[T any] struct {
		client         mqtt.Client
		encoding       Encoding[T]
		topic          *internal.TopicFilter
		shareName      string
		concurrency    uint
		reqCorrelation bool
		logger         log.Logger
		handler        interface {
			onMsg(context.Context, *mqtt.Message, *Message[T]) error
			onErr(context.Context, *mqtt.Message, error) error
		}

		sub    mqtt.Subscription
		done   func()
		active atomic.Bool
	}
)

func (l *listener[T]) register() error {
	handle, done := internal.Concurrent(l.concurrency, l.handle)

	// Make the subscription shared if specified.
	filter := l.topic.Filter()
	if l.shareName != "" {
		filter = "$share/" + l.shareName + "/" + filter
	}

	sub, err := l.client.Register(
		filter,
		func(ctx context.Context, pub *mqtt.Message) error {
			handle(ctx, pub)
			return nil
		},
	)
	if err != nil {
		done()
		return err
	}

	l.sub = sub
	l.done = done
	return nil
}

func (l *listener[T]) listen(ctx context.Context) error {
	if l.active.CompareAndSwap(false, true) {
		return l.sub.Update(
			ctx,
			mqtt.WithQoS(1),
			mqtt.WithNoLocal(l.shareName == ""),
		)
	}
	return nil
}

func (l *listener[T]) close() {
	if l.active.CompareAndSwap(true, false) {
		ctx := context.Background()
		if err := l.sub.Unsubscribe(ctx); err != nil {
			// Returning an error from a close function that is most likely to
			// be deferred is rarely useful, so just log it.
			l.logger.Err(ctx, err)
		}
	}
	l.done()
}

func (l *listener[T]) handle(ctx context.Context, pub *mqtt.Message) {
	msg := &Message[T]{}

	// The very first check must be the version, because if we don't support it,
	// nothing else is trustworthy.
	ver := pub.UserProperties[constants.ProtocolVersion]
	if !version.IsSupported(ver) {
		l.error(ctx, pub, &errors.Error{
			Message:                        "unsupported version",
			Kind:                           errors.UnsupportedRequestVersion,
			ProtocolVersion:                ver,
			SupportedMajorProtocolVersions: version.Supported,
		})
		return
	}

	if l.reqCorrelation && len(pub.CorrelationData) == 0 {
		l.error(ctx, pub, &errors.Error{
			Message:    "correlation data missing",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.CorrelationData,
		})
		return
	}
	if len(pub.CorrelationData) != 0 {
		correlationData, err := uuid.FromBytes(pub.CorrelationData)
		if err != nil {
			l.error(ctx, pub, &errors.Error{
				Message:    "correlation data is not a valid UUID",
				Kind:       errors.HeaderInvalid,
				HeaderName: constants.CorrelationData,
			})
			return
		}
		msg.CorrelationData = correlationData.String()
	}

	ts := pub.UserProperties[constants.Timestamp]
	if ts != "" {
		var err error
		msg.Timestamp, err = hlc.Parse(constants.Timestamp, ts)
		if err != nil {
			l.error(ctx, pub, err)
			return
		}
	}

	msg.Metadata = internal.PropToMetadata(pub.UserProperties)
	msg.TopicTokens = l.topic.Tokens(pub.Topic)

	if err := l.handler.onMsg(ctx, pub, msg); err != nil {
		l.error(ctx, pub, err)
		return
	}
}

// Handle payload manually, since it may be ignored on errors.
func (l *listener[T]) payload(pub *mqtt.Message) (T, error) {
	var zero T

	switch pub.PayloadFormat {
	case 0: // Do nothing; always valid.
	case 1:
		if l.encoding.PayloadFormat() == 0 {
			return zero, &errors.Error{
				Message:     "payload format indicator mismatch",
				Kind:        errors.HeaderInvalid,
				HeaderName:  constants.FormatIndicator,
				HeaderValue: fmt.Sprint(pub.PayloadFormat),
			}
		}
	default:
		return zero, &errors.Error{
			Message:     "payload format indicator invalid",
			Kind:        errors.HeaderInvalid,
			HeaderName:  constants.FormatIndicator,
			HeaderValue: fmt.Sprint(pub.PayloadFormat),
		}
	}

	if pub.ContentType != "" && l.encoding.ContentType() != "" &&
		pub.ContentType != l.encoding.ContentType() {
		return zero, &errors.Error{
			Message:     "content type mismatch",
			Kind:        errors.HeaderInvalid,
			HeaderName:  constants.ContentType,
			HeaderValue: pub.ContentType,
		}
	}

	return deserialize(l.encoding, pub.Payload)
}

func (l *listener[T]) ack(ctx context.Context, pub *mqtt.Message) {
	// Drop rather than returning, so we don't attempt to double-ack on failure.
	if err := pub.Ack(); err != nil {
		l.drop(ctx, pub, err)
	}
}

func (l *listener[T]) error(ctx context.Context, pub *mqtt.Message, err error) {
	// Drop the message if the error handler fails.
	if e := l.handler.onErr(ctx, pub, err); e != nil {
		l.drop(ctx, pub, err)
	}
}

func (l *listener[T]) drop(ctx context.Context, _ *mqtt.Message, err error) {
	l.logger.Err(ctx, err)
}

// Start listening to all underlying MQTT topics.
func (ls Listeners) Start(ctx context.Context) error {
	for _, l := range ls {
		if err := l.Start(ctx); err != nil {
			return err
		}
	}
	return nil
}

// Close all underlying MQTT topics and free resources.
func (ls Listeners) Close() {
	for _, l := range ls {
		l.Close()
	}
}
