// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
)

type (
	// TelemetryReceiver provides the ability to handle the receipt of a single
	// telemetry.
	TelemetryReceiver[T any] struct {
		listener  *listener[T]
		handler   TelemetryHandler[T]
		manualAck bool
		timeout   *internal.Timeout
		log       log.Logger
	}

	// TelemetryReceiverOption represents a single telemetry receiver option.
	TelemetryReceiverOption interface {
		telemetryReceiver(*TelemetryReceiverOptions)
	}

	// TelemetryReceiverOptions are the resolved telemetry receiver options.
	TelemetryReceiverOptions struct {
		ManualAck bool

		Concurrency uint
		Timeout     time.Duration
		ShareName   string

		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// TelemetryHandler is the user-provided implementation of a single
	// telemetry event handler. It is treated as blocking; all parallelism is
	// handled by the library. This *must* be thread-safe.
	TelemetryHandler[T any] = func(context.Context, *TelemetryMessage[T]) error

	// TelemetryMessage contains per-message data and methods that are exposed
	// to the telemetry handlers.
	TelemetryMessage[T any] struct {
		Message[T]

		// Ack provides a function to manually ack if enabled and if possible;
		// it will be nil otherwise. Note that, since QoS 0 messages cannot be
		// acked, this will be nil in this case even if manual ack is enabled.
		Ack func()
	}

	// WithManualAck indicates that the handler is responsible for manually
	// acking the telemetry message.
	WithManualAck bool
)

const (
	telemetryReceiverComponentName = "telemetry receiver"
	telemetryReceiverErrStr        = "telemetry receipt"
)

// NewTelemetryReceiver creates a new telemetry receiver.
func NewTelemetryReceiver[T any](
	app *Application,
	client MqttClient,
	encoding Encoding[T],
	topicPattern string,
	handler TelemetryHandler[T],
	opt ...TelemetryReceiverOption,
) (tr *TelemetryReceiver[T], err error) {
	var opts TelemetryReceiverOptions
	opts.Apply(opt)

	logger := log.Wrap(opts.Logger, app.log)
	defer func() { err = errutil.Return(err, logger, true) }()

	if err := errutil.ValidateNonNil(map[string]any{
		"client":   client,
		"encoding": encoding,
		"handler":  handler,
	}); err != nil {
		return nil, err
	}

	to := &internal.Timeout{
		Duration: opts.Timeout,
		Name:     "ExecutionTimeout",
		Text:     telemetryReceiverErrStr,
	}
	if err := to.Validate(); err != nil {
		return nil, err
	}

	if err := internal.ValidateShareName(opts.ShareName); err != nil {
		return nil, err
	}

	tp, err := internal.NewTopicPattern(
		"topicPattern",
		topicPattern,
		opts.TopicTokens,
		opts.TopicNamespace,
	)
	if err != nil {
		return nil, err
	}

	tf, err := tp.Filter()
	if err != nil {
		return nil, err
	}

	tr = &TelemetryReceiver[T]{
		handler:   handler,
		manualAck: opts.ManualAck,
		timeout:   to,
		log:       logger,
	}
	tr.listener = &listener[T]{
		app:              app,
		client:           client,
		encoding:         encoding,
		topic:            tf,
		shareName:        opts.ShareName,
		concurrency:      opts.Concurrency,
		supportedVersion: version.TelemetrySupported,
		log:              logger,
		handler:          tr,
	}

	tr.listener.register()
	return tr, nil
}

// Start listening to the MQTT telemetry topic.
func (tr *TelemetryReceiver[T]) Start(ctx context.Context) error {
	return tr.listener.start(ctx, telemetryReceiverComponentName)
}

// Close the telemetry receiver to free its resources.
func (tr *TelemetryReceiver[T]) Close() {
	tr.listener.close(telemetryReceiverComponentName)
}

func (tr *TelemetryReceiver[T]) onMsg(
	ctx context.Context,
	pub *mqtt.Message,
	msg *Message[T],
) error {
	tr.log.Debug(ctx, "telemetry received", slog.String("topic", pub.Topic))

	message := &TelemetryMessage[T]{Message: *msg}
	var err error

	message.Payload, err = tr.listener.payload(msg)
	if err != nil {
		return err
	}

	if tr.manualAck && pub.QoS > 0 {
		message.Ack = pub.Ack
	}

	handlerCtx, cancel := tr.timeout.Context(ctx)
	defer cancel()

	if err := tr.handle(handlerCtx, message); err != nil {
		return err
	}

	tr.ack(ctx, pub)
	return nil
}

func (tr *TelemetryReceiver[T]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	defer tr.ack(ctx, pub)

	// Strip off any no-return flags (without extra logging, since we're about
	// to drop the message anyways).
	if no, e := errutil.IsNoReturn(err); no {
		return e
	}

	return err
}

// Call handler with panic catch.
func (tr *TelemetryReceiver[T]) handle(
	ctx context.Context,
	msg *TelemetryMessage[T],
) error {
	rchan := make(chan error)

	// TODO: This goroutine will leak if the handler blocks without respecting
	// the context. This is a known limitation to align to the C# behavior, and
	// should be changed if that behavior is revisited.
	go func() {
		var err error
		defer func() {
			if ePanic := recover(); ePanic != nil {
				err = &errors.Remote{
					Message: fmt.Sprint(ePanic),
					Kind:    errors.ExecutionError{},
				}
			}

			select {
			case rchan <- err:
			case <-ctx.Done():
			}
		}()

		err = tr.handler(ctx, msg)
		if e := errutil.Context(ctx, telemetryReceiverErrStr); e != nil {
			// An error from the context overrides any return value.
			err = e
		} else if err != nil {
			err = &errors.Remote{
				Message: err.Error(),
				Kind:    errors.ExecutionError{},
			}
		}
	}()

	select {
	case err := <-rchan:
		return err
	case <-ctx.Done():
		return errutil.Context(ctx, telemetryReceiverErrStr)
	}
}

// Ack the telemetry if automatic and log it.
func (tr *TelemetryReceiver[T]) ack(
	ctx context.Context,
	pub *mqtt.Message,
) {
	if !tr.manualAck && pub.QoS > 0 {
		pub.Ack()
		tr.log.Debug(ctx, "telemetry acked", slog.String("topic", pub.Topic))
	}
}

// Apply resolves the provided list of options.
func (o *TelemetryReceiverOptions) Apply(
	opts []TelemetryReceiverOption,
	rest ...TelemetryReceiverOption,
) {
	for opt := range options.Apply[TelemetryReceiverOption](opts, rest...) {
		opt.telemetryReceiver(o)
	}
}

// ApplyOptions filters and resolves the provided list of options.
func (o *TelemetryReceiverOptions) ApplyOptions(opts []Option, rest ...Option) {
	for opt := range options.Apply[TelemetryReceiverOption](opts, rest...) {
		opt.telemetryReceiver(o)
	}
}

func (o *TelemetryReceiverOptions) telemetryReceiver(
	opt *TelemetryReceiverOptions,
) {
	if o != nil {
		*opt = *o
	}
}

func (*TelemetryReceiverOptions) option() {}

func (o WithManualAck) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.ManualAck = bool(o)
}

func (WithManualAck) option() {}
