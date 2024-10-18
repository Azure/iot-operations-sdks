// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
)

type (
	// TelemetrySender provides the ability to send a single telemetry.
	TelemetrySender[T any] struct {
		client    MqttClient
		publisher *publisher[T]
	}

	// TelemetrySenderOption represents a single telemetry sender option.
	TelemetrySenderOption interface {
		telemetrySender(*TelemetrySenderOptions)
	}

	// TelemetrySenderOptions are the resolved telemetry sender options.
	TelemetrySenderOptions struct {
		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// SendOption represent a single per-send option.
	SendOption interface{ send(*SendOptions) }

	// SendOptions are the resolved per-send options.
	SendOptions struct {
		Retain bool

		MessageExpiry uint32
		TopicTokens   map[string]string
		Metadata      map[string]string
	}

	// WithRetain indicates that the telemetry event should be retained by the
	// broker.
	WithRetain bool
)

// NewTelemetrySender creates a new telemetry sender.
func NewTelemetrySender[T any](
	client MqttClient,
	encoding Encoding[T],
	topic string,
	opt ...TelemetrySenderOption,
) (ts *TelemetrySender[T], err error) {
	defer func() { err = errutil.Return(err, true) }()

	var opts TelemetrySenderOptions
	opts.Apply(opt)

	if err := errutil.ValidateNonNil(map[string]any{
		"client":   client,
		"encoding": encoding,
	}); err != nil {
		return nil, err
	}

	tp, err := internal.NewTopicPattern(
		"topic",
		topic,
		opts.TopicTokens,
		opts.TopicNamespace,
	)
	if err != nil {
		return nil, err
	}

	ts = &TelemetrySender[T]{
		client: client,
	}
	ts.publisher = &publisher[T]{
		encoding: encoding,
		topic:    tp,
	}

	return ts, nil
}

// Send emits the telemetry. This will block until the message is ack'd.
func (ts *TelemetrySender[T]) Send(
	ctx context.Context,
	val T,
	opt ...SendOption,
) (err error) {
	shallow := true
	defer func() { err = errutil.Return(err, shallow) }()

	var opts SendOptions
	opts.Apply(opt)

	correlationData, err := errutil.NewUUID()
	if err != nil {
		return err
	}

	msg := &Message[T]{
		CorrelationData: correlationData,
		Payload:         val,
		Metadata:        opts.Metadata,
	}
	pub, err := ts.publisher.build(msg, opts.TopicTokens, opts.MessageExpiry)
	if err != nil {
		return err
	}

	pub.Retain = opts.Retain
	pub.UserProperties[constants.SenderClientID] = ts.client.ID()

	shallow = false
	_, err = ts.client.Publish(ctx, pub.Topic, pub.Payload, &pub.PublishOptions)
	return err
}

// Apply resolves the provided list of options.
func (o *TelemetrySenderOptions) Apply(
	opts []TelemetrySenderOption,
	rest ...TelemetrySenderOption,
) {
	for opt := range options.Apply[TelemetrySenderOption](opts, rest...) {
		opt.telemetrySender(o)
	}
}

// ApplyOptions filters and resolves the provided list of options.
func (o *TelemetrySenderOptions) ApplyOptions(opts []Option, rest ...Option) {
	for opt := range options.Apply[TelemetrySenderOption](opts, rest...) {
		opt.telemetrySender(o)
	}
}

func (o *TelemetrySenderOptions) telemetrySender(opt *TelemetrySenderOptions) {
	if o != nil {
		*opt = *o
	}
}

func (*TelemetrySenderOptions) option() {}

// Apply resolves the provided list of options.
func (o *SendOptions) Apply(
	opts []SendOption,
	rest ...SendOption,
) {
	for opt := range options.Apply[SendOption](opts, rest...) {
		opt.send(o)
	}
}

func (o *SendOptions) send(opt *SendOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithRetain) send(opt *SendOptions) {
	opt.Retain = bool(o)
}
