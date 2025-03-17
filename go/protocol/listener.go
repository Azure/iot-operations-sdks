// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"log/slog"
	"strings"
	"sync/atomic"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
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
		app              *Application
		client           MqttClient
		encoding         Encoding[T]
		topic            *internal.TopicFilter
		shareName        string
		concurrency      uint
		reqCorrelation   bool
		supportedVersion []int
		log              log.Logger
		handler          interface {
			onMsg(context.Context, *mqtt.Message, *Message[T]) error
			onErr(context.Context, *mqtt.Message, error) error
		}

		done   func()
		active atomic.Bool
	}

	message[T any] struct {
		Mqtt *mqtt.Message
		Message[T]
	}
)

func (l *listener[T]) register() {
	handle, stop := internal.Concurrent(l.concurrency, l.handle)
	done := l.client.RegisterMessageHandler(
		func(ctx context.Context, m *mqtt.Message) {
			msg := &message[T]{Mqtt: m}
			var match bool
			msg.TopicTokens, match = l.topic.Tokens(m.Topic)
			if match {
				handle(ctx, msg)
			} else {
				m.Ack()
			}
		},
	)
	l.done = func() {
		done()
		stop()
	}
}

func (l *listener[T]) filter() string {
	// Make the subscription shared if specified.
	if l.shareName != "" {
		return "$share/" + l.shareName + "/" + l.topic.Filter()
	}
	return l.topic.Filter()
}

func (l *listener[T]) start(ctx context.Context, name string) error {
	if l.active.CompareAndSwap(false, true) {
		filter := l.filter()
		ack, err := l.client.Subscribe(
			ctx,
			filter,
			mqtt.WithQoS(1),
			mqtt.WithNoLocal(l.shareName == ""),
		)
		if err := errutil.Mqtt(ctx, "subscribe", ack, err); err != nil {
			l.log.Warn(ctx, err)
			return err
		}
		l.log.Info(ctx, name+" started", slog.String("topic", filter))
	}
	return nil
}

func (l *listener[T]) close(name string) {
	ctx := context.Background()

	if l.active.CompareAndSwap(true, false) {
		filter := l.filter()
		if ack, err := l.client.Unsubscribe(ctx, filter); err != nil {
			// Returning an error from a close function that is most likely to
			// be deferred is rarely useful, so just log it.
			l.log.Error(ctx, errutil.Mqtt(ctx, "unsubscribe", ack, err))
		}
		l.log.Info(ctx, name+" closed", slog.String("topic", filter))
	}
	l.done()
}

func (l *listener[T]) handle(ctx context.Context, msg *message[T]) {
	// The very first check must be the version, because if we don't support it,
	// nothing else is trustworthy.
	ver := msg.Mqtt.UserProperties[constants.ProtocolVersion]
	if !version.IsSupported(ver, l.supportedVersion) {
		l.error(ctx, msg.Mqtt, &errors.Remote{
			Message: "request version not supported",
			Kind: errors.UnsupportedVersion{
				ProtocolVersion:                ver,
				SupportedMajorProtocolVersions: l.supportedVersion,
			},
		})
		return
	}

	msg.ClientID = msg.Mqtt.UserProperties[constants.SourceID]

	if l.reqCorrelation && len(msg.Mqtt.CorrelationData) == 0 {
		l.error(ctx, msg.Mqtt, &errors.Remote{
			Message: "correlation data missing",
			Kind: errors.HeaderMissing{
				HeaderName: constants.CorrelationData,
			},
		})
		return
	}
	if len(msg.Mqtt.CorrelationData) != 0 {
		correlationData, err := uuid.FromBytes(msg.Mqtt.CorrelationData)
		if err != nil {
			l.error(ctx, msg.Mqtt, &errors.Remote{
				Message: "correlation data is not a valid UUID",
				Kind: errors.HeaderInvalid{
					HeaderName: constants.CorrelationData,
				},
			})
			return
		}
		msg.CorrelationData = correlationData.String()
	}

	ts := msg.Mqtt.UserProperties[constants.Timestamp]
	if ts != "" {
		var err error
		msg.Timestamp, err = l.app.hlc.Parse(constants.Timestamp, ts)
		if err != nil {
			l.error(ctx, msg.Mqtt, &errors.Remote{
				Message: "timestamp is not a valid RFC3339 timestamp",
				Kind: errors.HeaderInvalid{
					HeaderName:  constants.Timestamp,
					HeaderValue: ts,
				},
			})
			return
		}
		if err = l.app.hlc.Set(msg.Timestamp); err != nil {
			l.error(ctx, msg.Mqtt, err)
			return
		}
	}

	msg.Metadata = make(map[string]string, len(msg.Mqtt.UserProperties))
	for key, val := range msg.Mqtt.UserProperties {
		if !strings.HasPrefix(key, constants.Protocol) {
			msg.Metadata[key] = val
		}
	}

	msg.Data = &Data{
		msg.Mqtt.Payload,
		msg.Mqtt.ContentType,
		msg.Mqtt.PayloadFormat,
	}

	if err := l.handler.onMsg(ctx, msg.Mqtt, &msg.Message); err != nil {
		l.error(ctx, msg.Mqtt, err)
		return
	}
}

// Handle payload manually, since it may be ignored on errors.
func (l *listener[T]) payload(msg *Message[T]) (T, error) {
	return deserialize(l.encoding, msg.Data)
}

func (l *listener[T]) error(ctx context.Context, pub *mqtt.Message, err error) {
	// Drop the message if the error handler fails.
	if e := l.handler.onErr(ctx, pub, err); e != nil {
		l.drop(ctx, pub, err)
	}
}

func (l *listener[T]) drop(ctx context.Context, _ *mqtt.Message, err error) {
	// Log dropped messages as an error, because we have no other way of
	// communicating this to the user.
	l.log.Error(ctx, err)
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
