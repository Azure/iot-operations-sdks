package protocol

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/caching"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/log"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
)

type (
	// CommandExecutor provides the ability to execute a single command.
	CommandExecutor[Req any, Res any] struct {
		client    mqtt.Client
		listener  *listener[Req]
		publisher *publisher[Res]
		handler   CommandHandler[Req, Res]
		timeout   internal.Timeout
		cache     *caching.Cache
		ttl       time.Duration
	}

	// CommandExecutorOption represents a single command executor option.
	CommandExecutorOption interface{ commandExecutor(*CommandExecutorOptions) }

	// CommandExecutorOptions are the resolved command executor options.
	CommandExecutorOptions struct {
		Idempotent bool
		CacheTTL   time.Duration

		Concurrency      uint
		ExecutionTimeout time.Duration
		ShareName        string

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

		FencingToken hlc.HybridLogicalClock
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
	client mqtt.Client,
	requestEncoding Encoding[Req],
	responseEncoding Encoding[Res],
	requestTopic string,
	handler CommandHandler[Req, Res],
	opt ...CommandExecutorOption,
) (ce *CommandExecutor[Req, Res], err error) {
	defer func() { err = errutil.Return(err, true) }()

	var options CommandExecutorOptions
	options.Apply(opt)

	if !options.Idempotent && options.CacheTTL != 0 {
		return nil, &errors.Error{
			Message:       "CacheTTL must be zero for non-idempotent commands",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "CacheTTL",
			PropertyValue: options.CacheTTL,
		}
	}

	if options.CacheTTL < 0 {
		return nil, &errors.Error{
			Message:       "CacheTTL must not have a negative value",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "CacheTTL",
			PropertyValue: options.CacheTTL,
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

	to, err := internal.NewExecutionTimeout(
		options.ExecutionTimeout,
		commandExecutorErrStr,
	)
	if err != nil {
		return nil, err
	}

	if err := internal.ValidateShareName(options.ShareName); err != nil {
		return nil, err
	}

	reqTP, err := internal.NewTopicPattern(
		"requestTopic",
		requestTopic,
		options.TopicTokens,
		options.TopicNamespace,
	)
	if err != nil {
		return nil, err
	}

	reqTF, err := reqTP.Filter()
	if err != nil {
		return nil, err
	}

	ce = &CommandExecutor[Req, Res]{
		client:  client,
		handler: handler,
		timeout: to,
		cache:   caching.New(options.Idempotent, requestTopic),
		ttl:     options.CacheTTL,
	}
	ce.listener = &listener[Req]{
		client:      ce.client,
		encoding:    requestEncoding,
		topic:       reqTF,
		shareName:   options.ShareName,
		concurrency: options.Concurrency,
		logger:      log.Wrap(options.Logger),
		handler:     ce,
	}
	ce.publisher = &publisher[Res]{
		encoding: responseEncoding,
		topic:    internal.TopicPattern{Pattern: "-"},
	}

	return ce, nil
}

// Listen to the MQTT request topic. Returns a function to stop listening. Note
// that cancelling this context will cause the unsubscribe call to fail.
func (ce *CommandExecutor[Req, Res]) Listen(
	ctx context.Context,
) (func(), error) {
	return ce.listener.listen(ctx)
}

func (ce *CommandExecutor[Req, Res]) onMsg(
	ctx context.Context,
	pub *mqtt.Message,
	msg *Message[Req],
) error {
	if err := ignoreRequest(pub); err != nil {
		return err
	}

	if pub.MessageExpiry == 0 {
		return &errors.Error{
			Message:    "message expiry missing",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.MessageExpiry,
		}
	}

	var rpub *mqtt.Message
	var err error

	if r, ok := ce.cache.Get(pub); ok {
		rpub = r
	} else {
		req := &CommandRequest[Req]{Message: *msg}

		req.ClientID = pub.UserProperties[constants.InvokerClientID]
		if req.ClientID == "" {
			return &errors.Error{
				Message:    "invoker client ID missing",
				Kind:       errors.HeaderMissing,
				HeaderName: constants.InvokerClientID,
			}
		}

		ft := pub.UserProperties[constants.FencingToken]
		if ft != "" {
			req.FencingToken, err = hlc.Parse(constants.FencingToken, ft)
			if err != nil {
				return err
			}
		}

		req.Payload, err = ce.listener.payload(pub)
		if err != nil {
			return err
		}

		handlerCtx, cancel := ce.timeout(ctx)
		defer cancel()

		handlerCtx, cancel = internal.MessageExpiryTimeout(
			handlerCtx,
			pub.MessageExpiry,
			commandExecutorErrStr,
		)
		defer cancel()

		expiry := time.Duration(pub.MessageExpiry) * time.Second

		start := wallclock.Instance.Now().UTC()
		res, err := ce.handle(handlerCtx, req)
		if err != nil {
			return err
		}
		exec := time.Since(start)

		rpub, err = ce.build(pub, res, nil)
		if err != nil {
			return err
		}

		ce.cache.Set(pub, rpub, start.Add(expiry), start.Add(ce.ttl), exec)
	}

	defer ce.listener.ack(ctx, pub)
	err = ce.client.Publish(ctx, rpub.Topic, rpub.Payload, &rpub.PublishOptions)
	if err != nil {
		// If the publish fails onErr will also fail, so just drop the message.
		ce.listener.drop(ctx, pub, err)
	}
	return nil
}

func (ce *CommandExecutor[Req, Res]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	defer ce.listener.ack(ctx, pub)

	if e := ignoreRequest(pub); e != nil {
		return e
	}

	// If the error is a no-return error, don't send it.
	if no, e := errutil.IsNoReturn(err); no {
		return e
	}

	rpub, err := ce.build(pub, nil, err)
	if err != nil {
		return err
	}
	return ce.client.Publish(
		ctx,
		rpub.Topic,
		rpub.Payload,
		&rpub.PublishOptions,
	)
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
		if e := errors.Context(ctx, commandExecutorErrStr); e != nil {
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
		return nil, errors.Context(ctx, commandExecutorErrStr)
	}
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
	rpub, err := ce.publisher.build(msg, nil, 0)
	if err != nil {
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

// Respond is a shorthand to create a command response with required values and
// options set appropriately. Note that the response may be incomplete and will
// be filled out by the library after being returned.
func Respond[Res any](
	payload Res,
	opt ...RespondOption,
) (*CommandResponse[Res], error) {
	var options RespondOptions
	options.Apply(opt)

	// TODO: Valid metadata keys will be validated by the response publish, but
	// consider whether we also want to validate them here preemptively.

	return &CommandResponse[Res]{Message[Res]{
		Payload:  payload,
		Metadata: options.Metadata,
	}}, nil
}

// Apply resolves the provided list of options.
func (o *CommandExecutorOptions) Apply(
	opts []CommandExecutorOption,
	rest ...CommandExecutorOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.commandExecutor(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.commandExecutor(o)
		}
	}
}

// ApplyOptions filters and resolves the provided list of options.
func (o *CommandExecutorOptions) ApplyOptions(opts []Option, rest ...Option) {
	for _, opt := range opts {
		if op, ok := opt.(CommandExecutorOption); ok {
			op.commandExecutor(o)
		}
	}
	for _, opt := range rest {
		if op, ok := opt.(CommandExecutorOption); ok {
			op.commandExecutor(o)
		}
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
	for _, opt := range opts {
		if opt != nil {
			opt.respond(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.respond(o)
		}
	}
}

func (o *RespondOptions) respond(opt *RespondOptions) {
	if o != nil {
		*opt = *o
	}
}
