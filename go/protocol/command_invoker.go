// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/container"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/errutil"
)

type (
	// CommandInvoker provides the ability to invoke a single command.
	CommandInvoker[Req any, Res any] struct {
		client        MqttClient
		publisher     *publisher[Req]
		listener      *listener[Res]
		responseTopic *internal.TopicPattern

		pending container.SyncMap[string, commandPending[Res]]
	}

	// CommandInvokerOption represents a single command invoker option.
	CommandInvokerOption interface{ commandInvoker(*CommandInvokerOptions) }

	// CommandInvokerOptions are the resolved command invoker options.
	CommandInvokerOptions struct {
		ResponseTopic       func(string) string
		ResponseTopicPrefix string
		ResponseTopicSuffix string

		TopicNamespace string
		TopicTokens    map[string]string
		Logger         *slog.Logger
	}

	// InvokeOption represent a single per-invoke option.
	InvokeOption interface{ invoke(*InvokeOptions) }

	// InvokeOptions are the resolved per-invoke options.
	InvokeOptions struct {
		FencingToken hlc.HybridLogicalClock

		Timeout     time.Duration
		TopicTokens map[string]string
		Metadata    map[string]string
	}

	// WithResponseTopic specifies a translation function from the request topic
	// to the response topic. Note that this overrides any provided response
	// topic prefix or suffix.
	WithResponseTopic func(string) string

	// WithResponseTopicPrefix specifies a custom prefix for the response topic.
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
	client MqttClient,
	requestEncoding Encoding[Req],
	responseEncoding Encoding[Res],
	requestTopicPattern string,
	opt ...CommandInvokerOption,
) (ci *CommandInvoker[Req, Res], err error) {
	defer func() { err = errutil.Return(err, true) }()

	var opts CommandInvokerOptions
	opts.Apply(opt)

	if err := errutil.ValidateNonNil(map[string]any{
		"client":           client,
		"requestEncoding":  requestEncoding,
		"responseEncoding": responseEncoding,
	}); err != nil {
		return nil, err
	}

	// Generate the response topic based on the provided options.
	responseTopic := requestTopicPattern
	if opts.ResponseTopic != nil {
		responseTopic = opts.ResponseTopic(requestTopicPattern)
	} else {
		if opts.ResponseTopicPrefix != "" {
			err = internal.ValidateTopicPatternComponent(
				"responseTopicPrefix",
				"invalid response topic prefix",
				opts.ResponseTopicPrefix,
			)
			if err != nil {
				return nil, err
			}
			responseTopic = opts.ResponseTopicPrefix + "/" + responseTopic
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
			responseTopic = responseTopic + "/" + opts.ResponseTopicSuffix
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
		"responseTopic",
		responseTopic,
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
		client:        client,
		responseTopic: resTP,
		pending:       container.NewSyncMap[string, commandPending[Res]](),
	}
	ci.publisher = &publisher[Req]{
		encoding: requestEncoding,
		topic:    reqTP,
	}
	ci.listener = &listener[Res]{
		client:         ci.client,
		encoding:       responseEncoding,
		topic:          resTF,
		reqCorrelation: true,
		log:            log.Wrap(opts.Logger),
		handler:        ci,
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
	defer func() { err = errutil.Return(err, shallow) }()

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
	if err := expiry.Validate(errors.ArgumentInvalid); err != nil {
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

	pub.UserProperties[constants.InvokerClientID] = ci.client.ID()
	pub.UserProperties[constants.Partition] = ci.client.ID()
	if !opts.FencingToken.IsZero() {
		pub.UserProperties[constants.FencingToken] = opts.FencingToken.String()
	}
	pub.ResponseTopic, err = ci.responseTopic.Topic(opts.TopicTokens)
	if err != nil {
		return nil, err
	}

	listen, done := ci.initPending(string(pub.CorrelationData))
	defer done()

	shallow = false
	_, err = ci.client.Publish(ctx, pub.Topic, pub.Payload, &pub.PublishOptions)
	if err != nil {
		return nil, err
	}

	// If a message expiry was specified, also time out our own context, so that
	// we stop listening for a response when none will come.
	ctx, cancel := expiry.Context(ctx)
	defer cancel()

	select {
	case res := <-listen:
		return res.res, res.err
	case <-ctx.Done():
		return nil, errors.Context(ctx, commandInvokerErrStr)
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
	defer ci.listener.ack(ctx, pub)

	cdata := string(pub.CorrelationData)
	if pending, ok := ci.pending.Get(cdata); ok {
		select {
		case pending.ret <- commandReturn[Res]{res, err}:
		case <-pending.done:
		case <-ctx.Done():
		}
		return nil
	}

	return &errors.Error{
		Message:     "unrecognized correlation data",
		Kind:        errors.HeaderInvalid,
		HeaderName:  constants.CorrelationData,
		HeaderValue: cdata,
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
		msg.Payload, err = ci.listener.payload(pub)
		if err == nil {
			res = &CommandResponse[Res]{*msg}
		}
	}
	if e := ci.sendPending(ctx, pub, res, err); e != nil {
		// If sendPending fails onErr will also fail, so just drop the message.
		ci.listener.drop(ctx, pub, e)
	}
	return nil
}

func (ci *CommandInvoker[Req, Res]) onErr(
	ctx context.Context,
	pub *mqtt.Message,
	err error,
) error {
	// If we received a version error from the listener implementation rather
	// than the response message, it indicates a version *we* don't support.
	if e, ok := err.(*errors.Error); ok &&
		e.Kind == errors.UnsupportedRequestVersion {
		e.Kind = errors.UnsupportedResponseVersion
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

func (o WithResponseTopic) commandInvoker(opt *CommandInvokerOptions) {
	opt.ResponseTopic = o
}

func (WithResponseTopic) option() {}

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

func (o WithFencingToken) invoke(opt *InvokeOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}
