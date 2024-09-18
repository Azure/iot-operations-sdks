package protocol

import (
	"context"
	"fmt"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/log"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/google/uuid"
)

type (
	// Listener represents an object which will listen to a MQTT topic.
	Listener interface {
		Listen(context.Context) (func(), error)
	}

	// Provide the shared implementation details for the MQTT listeners.
	listener[T any] struct {
		client      mqtt.Client
		encoding    Encoding[T]
		topic       string
		shareName   string
		concurrency uint
		logger      log.Logger
		handler     interface {
			onMsg(context.Context, *mqtt.Message, *Message[T]) error
			onErr(context.Context, *mqtt.Message, error) error
		}
	}
)

func (l *listener[T]) listen(ctx context.Context) (func(), error) {
	handle, done := internal.Concurrent(l.concurrency, l.handle)

	// Make the subscription shared if specified.
	filter := l.topic
	if l.shareName != "" {
		filter = "$share/" + l.shareName + "/" + filter
	}

	sub, err := l.client.Subscribe(
		ctx,
		filter,
		func(ctx context.Context, pub *mqtt.Message) error {
			handle(ctx, pub)
			return nil
		},
		mqtt.WithQoS(1),
		mqtt.WithNoLocal(l.shareName == ""),
	)
	if err != nil {
		done()
		return nil, err
	}

	return func() {
		if err := sub.Unsubscribe(ctx); err != nil {
			// Returning an error from a close function that is most likely to
			// be deferred is rarely useful, so just log it.
			l.logger.Err(ctx, err)
		}
		done()
	}, nil
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

	if len(pub.CorrelationData) == 0 {
		l.error(ctx, pub, &errors.Error{
			Message:    "correlation data missing",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.CorrelationData,
		})
		return
	}
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

	ts := pub.UserProperties[constants.Timestamp]
	if ts != "" {
		msg.Timestamp, err = hlc.Parse(constants.Timestamp, ts)
	} else {
		msg.Timestamp, err = hlc.Get()
	}
	if err != nil {
		l.error(ctx, pub, err)
		return
	}

	msg.Metadata = internal.PropToMetadata(pub.UserProperties)

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
		if !l.encoding.IsUTF8() {
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

// Listen starts all of the provided listeners.
func Listen(ctx context.Context, listeners ...Listener) (func(), error) {
	done := make([]func(), 0, len(listeners))
	for _, l := range listeners {
		c, err := l.Listen(ctx)
		if err != nil {
			for _, fn := range done {
				fn()
			}
			return nil, err
		}
		done = append(done, c)
	}
	return func() {
		for _, fn := range done {
			fn()
		}
	}, nil
}
