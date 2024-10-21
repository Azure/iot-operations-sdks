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
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
)

type (
	// TelemetryReceiver provides the ability to handle the receipt of a single
	// telemetry.
	TelemetryReceiver[T any] struct {
		listener  *listener[T]
		handler   TelemetryHandler[T]
		manualAck bool
		timeout   internal.Timeout
	}

	// TelemetryReceiverOption represents a single telemetry receiver option.
	TelemetryReceiverOption interface {
		telemetryReceiver(*TelemetryReceiverOptions)
	}

	// TelemetryReceiverOptions are the resolved telemetry receiver options.
	TelemetryReceiverOptions struct {
		ManualAck bool

		Concurrency      uint
		ExecutionTimeout time.Duration
		ShareName        string

		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// TelemetryHandler is the user-provided implementation of a single
	// telemetry event handler. It is treated as blocking; all parallelism is
	// handled by the library. This *must* be thread-safe.
	TelemetryHandler[T any] func(context.Context, *TelemetryMessage[T]) error

	// TelemetryMessage contains per-message data and methods that are exposed
	// to the telemetry handlers.
	TelemetryMessage[T any] struct {
		Message[T]

		// Ack provides a function to manually ack if enabled; it will be nil
		// otherwise.
		Ack func() error
	}

	// WithManualAck indicates that the handler is responsible for manually
	// acking the telemetry message.
	WithManualAck bool
)

const telemetryReceiverErrStr = "telemetry receipt"

// NewTelemetryReceiver creates a new telemetry receiver.
func NewTelemetryReceiver[T any](
	client MqttClient,
	encoding Encoding[T],
	topic string,
	handler TelemetryHandler[T],
	opt ...TelemetryReceiverOption,
) (tr *TelemetryReceiver[T], err error) {
	defer func() { err = errutil.Return(err, true) }()

	var opts TelemetryReceiverOptions
	opts.Apply(opt)

	if err := errutil.ValidateNonNil(map[string]any{
		"client":   client,
		"encoding": encoding,
		"handler":  handler,
	}); err != nil {
		return nil, err
	}

	to, err := internal.NewExecutionTimeout(opts.ExecutionTimeout,
		"telemetry handler timed out",
	)
	if err != nil {
		return nil, err
	}

	if err := internal.ValidateShareName(opts.ShareName); err != nil {
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

	tf, err := tp.Filter()
	if err != nil {
		return nil, err
	}

	tr = &TelemetryReceiver[T]{
		handler:   handler,
		manualAck: opts.ManualAck,
		timeout:   to,
	}
	tr.listener = &listener[T]{
		client:      client,
		encoding:    encoding,
		topic:       tf,
		shareName:   opts.ShareName,
		concurrency: opts.Concurrency,
		log:         log.Wrap(opts.Logger),
		handler:     tr,
	}

	tr.listener.register()
	return tr, nil
}

// Start listening to the MQTT telemetry topic.
func (tr *TelemetryReceiver[T]) Start(ctx context.Context) error {
	return tr.listener.listen(ctx)
}

// Close the telemetry receiver to free its resources.
func (tr *TelemetryReceiver[T]) Close() {
	tr.listener.close()
}

func (tr *TelemetryReceiver[T]) onMsg(
	ctx context.Context,
	pub *mqtt.Message,
	msg *Message[T],
) error {
	message := &TelemetryMessage[T]{Message: *msg}
	var err error

	message.ClientID = pub.UserProperties[constants.SenderClientID]

	message.Payload, err = tr.listener.payload(pub)
	if err != nil {
		return err
	}

	if tr.manualAck {
		message.Ack = pub.Ack
	}

	handlerCtx, cancel := tr.timeout(ctx)
	defer cancel()

	if err := tr.handle(handlerCtx, message); err != nil {
		return err
	}

	if !tr.manualAck {
		tr.listener.ack(ctx, pub)
	}
	return nil
}

func (tr *TelemetryReceiver[T]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	if !tr.manualAck {
		tr.listener.ack(ctx, pub)
	}
	return errutil.Return(err, false)
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
				err = &errors.Error{
					Message:       fmt.Sprint(ePanic),
					Kind:          errors.ExecutionException,
					InApplication: true,
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
			if e, ok := err.(InvocationError); ok {
				err = &errors.Error{
					Message:       e.Message,
					Kind:          errors.InvocationException,
					InApplication: true,
					PropertyName:  e.PropertyName,
					PropertyValue: e.PropertyValue,
				}
			} else {
				err = &errors.Error{
					Message:       err.Error(),
					Kind:          errors.ExecutionException,
					InApplication: true,
				}
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
