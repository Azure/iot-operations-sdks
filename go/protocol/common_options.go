// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"log/slog"
	"maps"
	"time"
)

type (
	// WithConcurrency indicates how many handlers can execute in parallel.
	WithConcurrency uint

	// WithTimeout applies a context timeout to the message invocation or
	// handler execution, as appropriate.
	WithTimeout time.Duration

	// WithShareName connects this listener to a shared MQTT subscription.
	WithShareName string

	// WithTopicTokens specifies topic token values.
	WithTopicTokens map[string]string

	// WithTopicTokenNamespace specifies a namespace that will be prepended to
	// all previously-specified topic tokens. Topic tokens specified after this
	// option will not be namespaced, allowing this to differentiate user tokens
	// from system tokens.
	WithTopicTokenNamespace string

	// WithMetadata specifies user-provided metadata values.
	WithMetadata map[string]string

	// WithTopicNamespace specifies a namespace that will be prepended to the
	// topic.
	WithTopicNamespace string

	// This option is not used directly; see WithLogger below.
	withLogger struct{ *slog.Logger }
)

func (o WithConcurrency) commandExecutor(opt *CommandExecutorOptions) {
	opt.Concurrency = uint(o)
}

func (WithConcurrency) option() {}

func (o WithConcurrency) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.Concurrency = uint(o)
}

func (o WithTimeout) commandExecutor(opt *CommandExecutorOptions) {
	opt.Timeout = time.Duration(o)
}

func (o WithTimeout) invoke(opt *InvokeOptions) {
	opt.Timeout = time.Duration(o)
}

func (WithTimeout) option() {}

func (o WithTimeout) send(opt *SendOptions) {
	opt.Timeout = time.Duration(o)
}

func (o WithTimeout) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.Timeout = time.Duration(o)
}

func (o WithShareName) commandExecutor(opt *CommandExecutorOptions) {
	opt.ShareName = string(o)
}

func (WithShareName) option() {}

func (o WithShareName) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.ShareName = string(o)
}

func (o WithTopicNamespace) commandExecutor(opt *CommandExecutorOptions) {
	opt.TopicNamespace = string(o)
}

func (o WithTopicNamespace) commandInvoker(opt *CommandInvokerOptions) {
	opt.TopicNamespace = string(o)
}

func (WithTopicNamespace) option() {}

func (o WithTopicNamespace) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.TopicNamespace = string(o)
}

func (o WithTopicNamespace) telemetrySender(opt *TelemetrySenderOptions) {
	opt.TopicNamespace = string(o)
}

func (o WithTopicTokens) apply(tokens map[string]string) map[string]string {
	if tokens == nil {
		tokens = make(map[string]string, len(o))
	}
	maps.Copy(tokens, o)
	return tokens
}

func (o WithTopicTokens) commandExecutor(opt *CommandExecutorOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokens) commandInvoker(opt *CommandInvokerOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokens) invoke(opt *InvokeOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (WithTopicTokens) option() {}

func (o WithTopicTokens) send(opt *SendOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokens) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokens) telemetrySender(opt *TelemetrySenderOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokenNamespace) apply(
	tokens map[string]string,
) map[string]string {
	result := make(map[string]string, len(tokens))
	for token, value := range tokens {
		result[string(o)+token] = value
	}
	return result
}

func (o WithTopicTokenNamespace) commandExecutor(opt *CommandExecutorOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokenNamespace) commandInvoker(opt *CommandInvokerOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokenNamespace) invoke(opt *InvokeOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (WithTopicTokenNamespace) option() {}

func (o WithTopicTokenNamespace) send(opt *SendOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokenNamespace) telemetryReceiver(
	opt *TelemetryReceiverOptions,
) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithTopicTokenNamespace) telemetrySender(opt *TelemetrySenderOptions) {
	opt.TopicTokens = o.apply(opt.TopicTokens)
}

func (o WithMetadata) apply(values map[string]string) map[string]string {
	if values == nil {
		values = make(map[string]string, len(o))
	}
	maps.Copy(values, o)
	return values
}

func (o WithMetadata) invoke(opt *InvokeOptions) {
	opt.Metadata = o.apply(opt.Metadata)
}

func (o WithMetadata) send(opt *SendOptions) {
	opt.Metadata = o.apply(opt.Metadata)
}

func (o WithMetadata) respond(opt *RespondOptions) {
	opt.Metadata = o.apply(opt.Metadata)
}

// WithLogger enables logging with the provided slog logger.
func WithLogger(logger *slog.Logger) interface {
	Option
	ApplicationOption
	CommandExecutorOption
	CommandInvokerOption
	TelemetryReceiverOption
	TelemetrySenderOption
} {
	return withLogger{logger}
}

func (o withLogger) application(opt *ApplicationOptions) {
	opt.Logger = o.Logger
}

func (o withLogger) commandExecutor(opt *CommandExecutorOptions) {
	opt.Logger = o.Logger
}

func (o withLogger) commandInvoker(opt *CommandInvokerOptions) {
	opt.Logger = o.Logger
}

func (withLogger) option() {}

func (o withLogger) telemetryReceiver(opt *TelemetryReceiverOptions) {
	opt.Logger = o.Logger
}

func (o withLogger) telemetrySender(opt *TelemetrySenderOptions) {
	opt.Logger = o.Logger
}
