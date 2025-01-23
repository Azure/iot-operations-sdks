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
	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/caching"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
)

type (
	// CommandExecutor provides the ability to execute a single command.
	CommandExecutor[Req any, Res any] struct {
		listener  *listener[Req]
		publisher *publisher[Res]
		handler   CommandHandler[Req, Res]
		timeout   *internal.Timeout
		cache     *caching.Cache
		logger    log.Logger
	}

	// CommandExecutorOption represents a single command executor option.
	CommandExecutorOption interface{ commandExecutor(*CommandExecutorOptions) }

	// CommandExecutorOptions are the resolved command executor options.
	CommandExecutorOptions struct {
		Idempotent bool
		CacheTTL   time.Duration

		Concurrency uint
		Timeout     time.Duration
		ShareName   string

		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// CommandHandler is the user-provided implementation of a single command
	// execution. It is treated as blocking; all parallelism is handled by the
	// library. This *must* be thread-safe.
	CommandHandler[Req any, Res any] func(
		context.Context,
		*CommandRequest[Req],
	) (*CommandResponse[Res], error)

	// CommandRequest contains per-message data and methods that are exposed to
	// the command handlers.
	CommandRequest[Req any] struct {
		Message[Req]
	}

	// CommandResponse contains per-message data and methods that are returned
	// by the command handlers.
	CommandResponse[Res any] struct {
		Message[Res]
	}

	// WithIdempotent marks the command as idempotent.
	WithIdempotent bool

	// WithCacheTTL indicates how long results of this command will live in the
	// cache. This is only valid for idempotent commands.
	WithCacheTTL time.Duration

	// RespondOption represent a single per-response option.
	RespondOption interface{ respond(*RespondOptions) }

	// RespondOptions are the resolved per-response options.
	RespondOptions struct {
		Metadata map[string]string
	}
)

const commandExecutorErrStr = "command execution"

// NewCommandExecutor creates a new command executor.
func NewCommandExecutor[Req, Res any](
	client MqttClient,
	requestEncoding Encoding[Req],
	responseEncoding Encoding[Res],
	requestTopicPattern string,
	handler CommandHandler[Req, Res],
	opt ...CommandExecutorOption,
) (ce *CommandExecutor[Req, Res], err error) {
	defer func() { err = errutil.Return(err, ce.listener.log, true) }()

	var opts CommandExecutorOptions
	opts.Apply(opt)

	if !opts.Idempotent && opts.CacheTTL != 0 {
		return nil, &errors.Error{
			Message:       "CacheTTL must be zero for non-idempotent commands",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "CacheTTL",
			PropertyValue: opts.CacheTTL,
		}
	}

	if opts.CacheTTL < 0 {
		return nil, &errors.Error{
			Message:       "CacheTTL must not have a negative value",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "CacheTTL",
			PropertyValue: opts.CacheTTL,
		}
	}

	if err := errutil.ValidateNonNil(map[string]any{
		"client":           client,
		"requestEncoding":  requestEncoding,
		"responseEncoding": responseEncoding,
		"handler":          handler,
	}); err != nil {
		return nil, err
	}

	to := &internal.Timeout{
		Duration: opts.Timeout,
		Name:     "ExecutionTimeout",
		Text:     commandExecutorErrStr,
	}
	if err := to.Validate(errors.ConfigurationInvalid); err != nil {
		return nil, err
	}

	if err := internal.ValidateShareName(opts.ShareName); err != nil {
		return nil, err
	}

	reqTP, err := internal.NewTopicPattern(
		"requestTopicPattern",
		requestTopicPattern,
		opts.TopicTokens,
		opts.TopicNamespace,
	)
	if err != nil {
		return nil, err
	}

	reqTF, err := reqTP.Filter()
	if err != nil {
		return nil, err
	}

	ce = &CommandExecutor[Req, Res]{
		handler: handler,
		timeout: to,
		cache: caching.New(
			wallclock.Instance,
			opts.CacheTTL,
			requestTopicPattern,
		),
	}
	ce.listener = &listener[Req]{
		client:         client,
		encoding:       requestEncoding,
		topic:          reqTF,
		shareName:      opts.ShareName,
		concurrency:    opts.Concurrency,
		reqCorrelation: true,
		log:            log.Wrap(opts.Logger),
		handler:        ce,
	}
	ce.publisher = &publisher[Res]{
		client:   client,
		encoding: responseEncoding,
	}

	ce.listener.register()
	ce.listener.log.Debug(context.Background(), "Command executor created")
	return ce, nil
}

// Start listening to the MQTT request topic.
func (ce *CommandExecutor[Req, Res]) Start(ctx context.Context) error {
	ce.listener.log.Info(
		ctx,
		"Subscribing to MQTT request topic",
		slog.String("topic", ce.listener.topic.Filter()),
	)
	err := ce.listener.listen(ctx)
	return err
}

// Close the command executor to free its resources.
func (ce *CommandExecutor[Req, Res]) Close() {
	ce.listener.log.Info(
		context.Background(),
		"Unsubscribing from MQTT request topic",
		slog.String("topic", ce.listener.topic.Filter()),
	)
	ce.listener.close()
	ce.listener.log.Info(
		context.Background(),
		"Command executor shutdown complete",
	)
}

func (ce *CommandExecutor[Req, Res]) onMsg(
	ctx context.Context,
	pub *mqtt.Message,
	msg *Message[Req],
) error {
	ce.listener.log.Debug(
		ctx,
		"Request received",
		slog.String("correlationData", string(pub.CorrelationData)),
	)

	if err := ignoreRequest(pub); err != nil {
		ce.listener.log.Warn(
			ctx,
			err.Error(),
			slog.String(
				"message",
				"Ignoring request due to invalid or missing response topic",
			),
		)
		return err
	}

	if pub.MessageExpiry == 0 {
		ce.listener.log.Error(
			ctx,
			fmt.Errorf("Command has no expiry"),
			slog.String(
				"message",
				"Request will be rejected due to missing expiry",
			),
		)
		return &errors.Error{
			Message:    "message expiry missing",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.MessageExpiry,
		}
	}

	rpub, err := ce.cache.Exec(pub, func() (*mqtt.Message, error) {
		req := &CommandRequest[Req]{Message: *msg}
		var err error

		if msg.ClientID == "" {
			return nil, &errors.Error{
				Message:    "source client ID missing",
				Kind:       errors.HeaderMissing,
				HeaderName: constants.SourceID,
			}
		}

		req.Payload, err = ce.listener.payload(msg)
		if err != nil {
			return nil, err
		}

		handlerCtx, cancel := ce.timeout.Context(ctx)
		defer cancel()

		handlerCtx, cancel = pubTimeout(pub).Context(handlerCtx)
		defer cancel()

		res, err := ce.handle(handlerCtx, req)
		if err != nil {
			ce.listener.log.Warn(
				ctx,
				err.Error(),
				slog.String("message", "Ignoring request due to handler error"),
			)
			return nil, err
		}

		rpub, err := ce.build(pub, res, nil)
		if err != nil {
			ce.listener.log.Error(
				ctx,
				err,
				slog.String("message", "Failed to build response"),
			)
			return nil, err
		}

		return rpub, nil
	})
	if err != nil {
		return err
	}

	defer pub.Ack()
	ce.listener.log.Debug(
		ctx,
		"request acked",
		slog.String("correlationData", string(pub.CorrelationData)),
	)
	if rpub == nil {
		return nil
	}

	err = ce.publisher.publish(ctx, rpub)
	if err != nil {
		// If the publish fails onErr will also fail, so just drop the message.
		ce.listener.drop(ctx, pub, err)
	} else {
		ce.listener.log.Debug(ctx, "Response sent", slog.String("correlationData", string(pub.CorrelationData)))
	}
	return nil
}

func (ce *CommandExecutor[Req, Res]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	defer pub.Ack()

	if e := ignoreRequest(pub); e != nil {
		return e
	}

	// If the error is a no-return error, don't send it.
	if no, e := errutil.IsNoReturn(err); no {
		ce.listener.log.Warn(
			ctx,
			e.Error(),
			slog.String("message", "Ignoring request due to no-return error"),
		)
		return e
	}

	rpub, err := ce.build(pub, nil, err)
	if err != nil {
		return err
	}
	return ce.publisher.publish(ctx, rpub)
}

// Call handler with panic catch.
func (ce *CommandExecutor[Req, Res]) handle(
	ctx context.Context,
	req *CommandRequest[Req],
) (*CommandResponse[Res], error) {
	rchan := make(chan commandReturn[Res])

	// TODO: This goroutine will leak if the handler blocks without respecting
	// the context. This is a known limitation to align to the C# behavior, and
	// should be changed if that behavior is revisited.
	go func() {
		var ret commandReturn[Res]
		defer func() {
			if ePanic := recover(); ePanic != nil {
				ret.err = &errors.Error{
					Message:       fmt.Sprint(ePanic),
					Kind:          errors.ExecutionException,
					InApplication: true,
				}
			}

			select {
			case rchan <- ret:
			case <-ctx.Done():
			}
		}()

		ret.res, ret.err = ce.handler(ctx, req)
		if e := errutil.Context(ctx, commandExecutorErrStr); e != nil {
			// An error from the context overrides any return value.
			ret.err = e
		} else if ret.err != nil {
			if e, ok := ret.err.(InvocationError); ok {
				ret.err = &errors.Error{
					Message:       e.Message,
					Kind:          errors.InvocationException,
					InApplication: true,
					PropertyName:  e.PropertyName,
					PropertyValue: e.PropertyValue,
				}
			} else {
				ret.err = &errors.Error{
					Message:       ret.err.Error(),
					Kind:          errors.ExecutionException,
					InApplication: true,
				}
			}
		}
	}()

	select {
	case ret := <-rchan:
		return ret.res, ret.err
	case <-ctx.Done():
		ce.listener.log.Warn(
			ctx,
			"Command handler timed out",
			slog.String("message", "Ignoring request due to handler timeout"),
		)
		return nil, errutil.Context(ctx, commandExecutorErrStr)
	}
}

// Build the response publish packet.
func (ce *CommandExecutor[Req, Res]) build(
	pub *mqtt.Message,
	res *CommandResponse[Res],
	resErr error,
) (*mqtt.Message, error) {
	var msg *Message[Res]
	if res != nil {
		msg = &res.Message
	}
	rpub, err := ce.publisher.build(msg, nil, pubTimeout(pub))
	if err != nil {
		ce.listener.log.Error(
			context.Background(),
			err,
			slog.String("message", "Failed to build response"),
		)
		return nil, err
	}

	rpub.CorrelationData = pub.CorrelationData
	rpub.Topic = pub.ResponseTopic
	rpub.MessageExpiry = pub.MessageExpiry
	for key, val := range errutil.ToUserProp(resErr) {
		rpub.UserProperties[key] = val
	}

	return rpub, nil
}

// Check whether this message should be ignored and why.
func ignoreRequest(pub *mqtt.Message) error {
	if pub.ResponseTopic == "" {
		return &errors.Error{
			Message:    "missing response topic",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.ResponseTopic,
		}
	}
	if !internal.ValidTopic(pub.ResponseTopic) {
		return &errors.Error{
			Message:     "invalid response topic",
			Kind:        errors.HeaderInvalid,
			HeaderName:  constants.ResponseTopic,
			HeaderValue: pub.ResponseTopic,
		}
	}
	return nil
}

// Build a timeout based on the message's expiry.
func pubTimeout(pub *mqtt.Message) *internal.Timeout {
	return &internal.Timeout{
		Duration: time.Duration(pub.MessageExpiry) * time.Second,
		Name:     "MessageExpiry",
		Text:     commandExecutorErrStr,
	}
}

// Respond is a shorthand to create a command response with required values and
// options set appropriately. Note that the response may be incomplete and will
// be filled out by the library after being returned.
func Respond[Res any](
	payload Res,
	opt ...RespondOption,
) (*CommandResponse[Res], error) {
	var opts RespondOptions
	opts.Apply(opt)

	// TODO: Valid metadata keys will be validated by the response publish, but
	// consider whether we also want to validate them here preemptively.

	return &CommandResponse[Res]{Message[Res]{
		Payload:  payload,
		Metadata: opts.Metadata,
	}}, nil
}

// Apply resolves the provided list of options.
func (o *CommandExecutorOptions) Apply(
	opts []CommandExecutorOption,
	rest ...CommandExecutorOption,
) {
	for opt := range options.Apply[CommandExecutorOption](opts, rest...) {
		opt.commandExecutor(o)
	}
}

// ApplyOptions filters and resolves the provided list of options.
func (o *CommandExecutorOptions) ApplyOptions(opts []Option, rest ...Option) {
	for opt := range options.Apply[CommandExecutorOption](opts, rest...) {
		opt.commandExecutor(o)
	}
}

func (o *CommandExecutorOptions) commandExecutor(opt *CommandExecutorOptions) {
	if o != nil {
		*opt = *o
	}
}

func (*CommandExecutorOptions) option() {}

func (o WithIdempotent) commandExecutor(opt *CommandExecutorOptions) {
	opt.Idempotent = bool(o)
}

func (WithIdempotent) option() {}

func (o WithCacheTTL) commandExecutor(opt *CommandExecutorOptions) {
	opt.CacheTTL = time.Duration(o)
}

func (WithCacheTTL) option() {}

// Apply resolves the provided list of options.
func (o *RespondOptions) Apply(
	opts []RespondOption,
	rest ...RespondOption,
) {
	for opt := range options.Apply[RespondOption](opts, rest...) {
		opt.respond(o)
	}
}

func (o *RespondOptions) respond(opt *RespondOptions) {
	if o != nil {
		*opt = *o
	}
}
