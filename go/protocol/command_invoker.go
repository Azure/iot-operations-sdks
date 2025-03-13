// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/container"
	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
)

type (
	// CommandInvoker provides the ability to invoke a single command.
	CommandInvoker[Req any, Res any] struct {
		publisher     *publisher[Req]
		listener      *listener[Res]
		responseTopic *internal.TopicPattern

		pending container.SyncMap[string, commandPending[Res]]
	}

	// CommandInvokerOption represents a single command invoker option.
	CommandInvokerOption interface{ commandInvoker(*CommandInvokerOptions) }

	// CommandInvokerOptions are the resolved command invoker options.
	CommandInvokerOptions struct {
		ResponseTopicPattern string
		ResponseTopicPrefix  string
		ResponseTopicSuffix  string

		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// InvokeOption represent a single per-invoke option.
	InvokeOption interface{ invoke(*InvokeOptions) }

	// InvokeOptions are the resolved per-invoke options.
	InvokeOptions struct {
		Timeout     time.Duration
		TopicTokens map[string]string
		Metadata    map[string]string
	}

	// WithResponseTopicPattern specifies a custom response topic pattern. Note
	// that this overrides any provided response topic prefix or suffix.
	WithResponseTopicPattern string

	// WithResponseTopicPrefix specifies a custom prefix for the response topic.
	// If no response topic options are specified, this will default to a value
	// of "clients/<MQTT client ID>".
	WithResponseTopicPrefix string

	// WithResponseTopicSuffix specifies a custom suffix for the response topic.
	WithResponseTopicSuffix string

	// WithFencingToken provides a fencing token to be used by the executor.
	WithFencingToken hlc.HybridLogicalClock

	// Return values for an invocation, since they are received asynchronously.
	commandReturn[Res any] struct {
		res *CommandResponse[Res]
		err error
	}

	// A pair of return channel (to send the return values on) and done channel
	// (to prevent blocking when the invoker is no longer listening).
	commandPending[Res any] struct {
		ret  chan<- commandReturn[Res]
		done <-chan struct{}
	}
)

const commandInvokerErrStr = "command invocation"

// NewCommandInvoker creates a new command invoker.
func NewCommandInvoker[Req, Res any](
	app *Application,
	client MqttClient,
	requestEncoding Encoding[Req],
	responseEncoding Encoding[Res],
	requestTopicPattern string,
	opt ...CommandInvokerOption,
) (ci *CommandInvoker[Req, Res], err error) {
	var opts CommandInvokerOptions
	opts.Apply(opt)
	logger := log.Wrap(opts.Logger, app.log)

	defer func() { err = errutil.Return(err, logger, true) }()

	if err := errutil.ValidateNonNil(map[string]any{
		"client":           client,
		"requestEncoding":  requestEncoding,
		"responseEncoding": responseEncoding,
	}); err != nil {
		return nil, err
	}

	responseTopicPattern := opts.ResponseTopicPattern
	if responseTopicPattern == "" {
		responseTopicPattern = requestTopicPattern

		if opts.ResponseTopicPrefix != "" {
			err = internal.ValidateTopicPatternComponent(
				"responseTopicPrefix",
				"invalid response topic prefix",
				opts.ResponseTopicPrefix,
			)
			if err != nil {
				return nil, err
			}
			responseTopicPattern = opts.ResponseTopicPrefix + "/" + responseTopicPattern
		}
		if opts.ResponseTopicSuffix != "" {
			err = internal.ValidateTopicPatternComponent(
				"responseTopicSuffix",
				"invalid response topic suffix",
				opts.ResponseTopicSuffix,
			)
			if err != nil {
				return nil, err
			}
			responseTopicPattern = responseTopicPattern + "/" + opts.ResponseTopicSuffix
		}

		// If no options were provided, apply a well-known prefix. This ensures
		// that the response topic is different from the request topic and lets
		// us document this pattern for auth configuration. Note that this does
		// not use any topic tokens, since we cannot guarantee their existence.
		if opts.ResponseTopicPrefix == "" && opts.ResponseTopicSuffix == "" {
			responseTopicPattern = "clients/" + client.ID() + "/" + requestTopicPattern
		}
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

	resTP, err := internal.NewTopicPattern(
		"responseTopicPattern",
		responseTopicPattern,
		opts.TopicTokens,
		opts.TopicNamespace,
	)
	if err != nil {
		return nil, err
	}

	resTF, err := resTP.Filter()
	if err != nil {
		return nil, err
	}

	ci = &CommandInvoker[Req, Res]{
		responseTopic: resTP,
		pending:       container.NewSyncMap[string, commandPending[Res]](),
	}
	ci.publisher = &publisher[Req]{
		app:      app,
		client:   client,
		encoding: requestEncoding,
		version:  version.RPC,
		topic:    reqTP,
	}
	ci.listener = &listener[Res]{
		app:              app,
		client:           client,
		encoding:         responseEncoding,
		topic:            resTF,
		reqCorrelation:   true,
		supportedVersion: version.RPCSupported,
		log:              logger,
		handler:          ci,
	}

	ci.listener.register()
	return ci, nil
}

// Invoke calls the command. This call will block until the command returns; any
// desired parallelism between invocations should be handled by the caller using
// normal Go constructs.
func (ci *CommandInvoker[Req, Res]) Invoke(
	ctx context.Context,
	req Req,
	opt ...InvokeOption,
) (res *CommandResponse[Res], err error) {
	shallow := true
	defer func() { err = errutil.Return(err, ci.listener.log, shallow) }()

	var opts InvokeOptions
	opts.Apply(opt)

	timeout := opts.Timeout
	if timeout == 0 {
		timeout = DefaultTimeout
	}

	expiry := &internal.Timeout{
		Duration: timeout,
		Name:     "MessageExpiry",
		Text:     commandInvokerErrStr,
	}
	if err := expiry.Validate(); err != nil {
		return nil, err
	}

	correlationData, err := errutil.NewUUID()
	if err != nil {
		return nil, err
	}

	msg := &Message[Req]{
		CorrelationData: correlationData,
		Payload:         req,
		Metadata:        opts.Metadata,
	}
	pub, err := ci.publisher.build(msg, opts.TopicTokens, expiry)
	if err != nil {
		return nil, err
	}

	pub.UserProperties[constants.Partition] = ci.publisher.client.ID()
	pub.ResponseTopic, err = ci.responseTopic.Topic(opts.TopicTokens)
	if err != nil {
		return nil, err
	}

	listen, done := ci.initPending(string(pub.CorrelationData))
	defer done()

	shallow = false
	err = ci.publisher.publish(ctx, pub)
	if err != nil {
		return nil, err
	}

	ci.listener.log.Debug(
		ctx,
		"request sent",
		slog.String("correlation_data", correlationData),
	)

	// If a message expiry was specified, also time out our own context, so that
	// we stop listening for a response when none will come.
	ctx, cancel := expiry.Context(ctx)
	defer cancel()

	select {
	case res := <-listen:
		return res.res, res.err
	case <-ctx.Done():
		return nil, errutil.Context(ctx, commandInvokerErrStr)
	}
}

// Initialize channels for a pending response.
func (ci *CommandInvoker[Req, Res]) initPending(
	correlation string,
) (<-chan commandReturn[Res], func()) {
	ret := make(chan commandReturn[Res])
	done := make(chan struct{})
	ci.pending.Set(correlation, commandPending[Res]{ret, done})
	return ret, func() {
		ci.pending.Del(correlation)
		close(done)
	}
}

// Send return values to a pending response.
func (ci *CommandInvoker[Req, Res]) sendPending(
	ctx context.Context,
	pub *mqtt.Message,
	res *CommandResponse[Res],
	err error,
) error {
	defer pub.Ack()

	cdata := string(pub.CorrelationData)
	if pending, ok := ci.pending.Get(cdata); ok {
		select {
		case pending.ret <- commandReturn[Res]{res, err}:
			ci.listener.log.Debug(
				ctx,
				"request ack received",
				slog.String("correlation_data", cdata),
			)
		case <-pending.done:
		case <-ctx.Done():
		}
		ci.listener.log.Debug(
			ctx,
			"response acked",
			slog.String("correlation_data", cdata),
		)
		return nil
	}

	ci.listener.log.Debug(
		ctx,
		"response not for this invoker",
		slog.String("correlation_data", cdata),
	)
	return &errors.Client{
		Message: "unrecognized correlation data",
		Kind: errors.HeaderInvalid{
			HeaderName:  constants.CorrelationData,
			HeaderValue: cdata,
		},
	}
}

// Start listening to the response topic(s). Must be called before any calls to
// Invoke.
func (ci *CommandInvoker[Req, Res]) Start(ctx context.Context) error {
	return ci.listener.listen(ctx)
}

// Close the command invoker to free its resources.
func (ci *CommandInvoker[Req, Res]) Close() {
	ci.listener.close()
}

func (ci *CommandInvoker[Req, Res]) onMsg(
	ctx context.Context,
	pub *mqtt.Message,
	msg *Message[Res],
) error {
	var res *CommandResponse[Res]
	err := errutil.FromUserProp(pub.UserProperties)
	if err == nil {
		msg.Payload, err = ci.listener.payload(msg)
		if err == nil {
			res = &CommandResponse[Res]{*msg}
		}
	}
	if e := ci.sendPending(ctx, pub, res, err); e != nil {
		// If sendPending fails onErr will also fail, so just drop the message.
		ci.listener.drop(ctx, pub, e)
	}
	ci.listener.log.Debug(
		ctx,
		"response received",
		slog.String("correlation_data", string(pub.CorrelationData)),
	)
	return nil
}

func (ci *CommandInvoker[Req, Res]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	if re, ok := err.(*errors.Remote); ok {
		// "Remote" errors returned in onErr for the invoker are actually local.
		ce := &errors.Client{Message: re.Message, Kind: re.Kind}
		if _, ok := ce.Kind.(errors.UnsupportedVersion); ok {
			// A version error from the listener implementation indicates a
			// *response* version we don't support, so update the message.
			ce.Message = "response version is not supported"
		}
		err = ce
	}
	return ci.sendPending(ctx, pub, nil, err)
}

// Apply resolves the provided list of options.
func (o *CommandInvokerOptions) Apply(
	opts []CommandInvokerOption,
	rest ...CommandInvokerOption,
) {
	for opt := range options.Apply[CommandInvokerOption](opts, rest...) {
		opt.commandInvoker(o)
	}
}

// ApplyOptions filters and resolves the provided list of options.
func (o *CommandInvokerOptions) ApplyOptions(opts []Option, rest ...Option) {
	for opt := range options.Apply[CommandInvokerOption](opts, rest...) {
		opt.commandInvoker(o)
	}
}

func (o *CommandInvokerOptions) commandInvoker(opt *CommandInvokerOptions) {
	if o != nil {
		*opt = *o
	}
}

func (*CommandInvokerOptions) option() {}

func (o WithResponseTopicPattern) commandInvoker(opt *CommandInvokerOptions) {
	opt.ResponseTopicPattern = string(o)
}

func (WithResponseTopicPattern) option() {}

func (o WithResponseTopicPrefix) commandInvoker(opt *CommandInvokerOptions) {
	opt.ResponseTopicPrefix = string(o)
}

func (WithResponseTopicPrefix) option() {}

func (o WithResponseTopicSuffix) commandInvoker(opt *CommandInvokerOptions) {
	opt.ResponseTopicSuffix = string(o)
}

func (WithResponseTopicSuffix) option() {}

// Apply resolves the provided list of options.
func (o *InvokeOptions) Apply(
	opts []InvokeOption,
	rest ...InvokeOption,
) {
	for opt := range options.Apply[InvokeOption](opts, rest...) {
		opt.invoke(o)
	}
}

func (o *InvokeOptions) invoke(opt *InvokeOptions) {
	if o != nil {
		*opt = *o
	}
}
