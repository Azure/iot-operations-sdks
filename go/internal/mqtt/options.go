// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "github.com/Azure/iot-operations-sdks/go/internal/options"

type (
	// SubscribeOptions are the resolved subscribe options.
	SubscribeOptions struct {
		NoLocal        bool
		QoS            byte
		Retain         bool
		RetainHandling byte
		UserProperties map[string]string
	}

	// SubscribeOption represents a single subscribe option.
	SubscribeOption interface{ subscribe(*SubscribeOptions) }

	// UnsubscribeOptions are the resolve unsubscribe options.
	UnsubscribeOptions struct {
		UserProperties map[string]string
	}

	// UnsubscribeOption represents a single unsubscribe option.
	UnsubscribeOption interface{ unsubscribe(*UnsubscribeOptions) }

	// PublishOptions are the resolved publish options.
	PublishOptions struct {
		ContentType     string
		CorrelationData []byte
		MessageExpiry   uint32
		PayloadFormat   byte
		QoS             byte
		ResponseTopic   string
		Retain          bool
		UserProperties  map[string]string
	}

	// PublishOption represents a single publish option.
	PublishOption interface{ publish(*PublishOptions) }

	// WithContentType sets the content type for the publish.
	WithContentType string

	// WithCorrelationData sets the correlation data for the publish.
	WithCorrelationData []byte

	// WithMessageExpiry sets the message expiry interval for the publish.
	WithMessageExpiry uint32

	// WithNoLocal sets the no local flag for the subscription.
	WithNoLocal bool

	// WithPayloadFormat sets the payload format indicator for the publish.
	WithPayloadFormat byte

	// WithQoS sets the QoS level for the publish or subscribe.
	WithQoS byte

	// WithResponseTopic sets the response topic for the publish.
	WithResponseTopic string

	// WithRetain sets the retain flag for the publish or the retain-as-publish
	// flag for the subscribe.
	WithRetain bool

	// WithRetainHandling specifies the handling of retained messages on the
	// subscribe.
	WithRetainHandling byte

	// WithUserProperties sets the user properties for the publish or subscribe.
	WithUserProperties map[string]string
)

func (o WithContentType) publish(opt *PublishOptions) {
	opt.ContentType = string(o)
}

func (o WithCorrelationData) publish(opt *PublishOptions) {
	opt.CorrelationData = []byte(o)
}

func (o WithMessageExpiry) publish(opt *PublishOptions) {
	opt.MessageExpiry = uint32(o)
}

func (o WithNoLocal) subscribe(opt *SubscribeOptions) {
	opt.NoLocal = bool(o)
}

func (o WithPayloadFormat) publish(opt *PublishOptions) {
	opt.PayloadFormat = byte(o)
}

func (o WithQoS) publish(opt *PublishOptions) {
	opt.QoS = byte(o)
}

func (o WithQoS) subscribe(opt *SubscribeOptions) {
	opt.QoS = byte(o)
}

func (o WithResponseTopic) publish(opt *PublishOptions) {
	opt.ResponseTopic = string(o)
}

func (o WithRetain) publish(opt *PublishOptions) {
	opt.Retain = bool(o)
}

func (o WithRetain) subscribe(opt *SubscribeOptions) {
	opt.Retain = bool(o)
}

func (o WithRetainHandling) subscribe(opt *SubscribeOptions) {
	opt.RetainHandling = byte(o)
}

func (o WithUserProperties) apply(user map[string]string) map[string]string {
	if user == nil {
		user = make(map[string]string, len(user))
	}
	for key, val := range o {
		user[key] = val
	}
	return user
}

func (o WithUserProperties) publish(opt *PublishOptions) {
	opt.UserProperties = o.apply(opt.UserProperties)
}

func (o WithUserProperties) subscribe(opt *SubscribeOptions) {
	opt.UserProperties = o.apply(opt.UserProperties)
}

func (o WithUserProperties) unsubscribe(opt *UnsubscribeOptions) {
	opt.UserProperties = o.apply(opt.UserProperties)
}

// Apply resolves the provided list of options.
func (o *SubscribeOptions) Apply(
	opts []SubscribeOption,
	rest ...SubscribeOption,
) {
	for opt := range options.Apply[SubscribeOption](opts, rest...) {
		opt.subscribe(o)
	}
}

// Assign non-nil options.
func (o *SubscribeOptions) subscribe(opt *SubscribeOptions) {
	if o != nil {
		*opt = *o
	}
}

// Apply resolves the provided list of options.
func (o *UnsubscribeOptions) Apply(
	opts []UnsubscribeOption,
	rest ...UnsubscribeOption,
) {
	for opt := range options.Apply[UnsubscribeOption](opts, rest...) {
		opt.unsubscribe(o)
	}
}

// Assign non-nil options.
func (o *UnsubscribeOptions) unsubscribe(opt *UnsubscribeOptions) {
	if o != nil {
		*opt = *o
	}
}

// Apply resolves the provided list of options.
func (o *PublishOptions) Apply(
	opts []PublishOption,
	rest ...PublishOption,
) {
	for opt := range options.Apply[PublishOption](opts, rest...) {
		opt.publish(o)
	}
}

// Assign non-nil options.
func (o *PublishOptions) publish(opt *PublishOptions) {
	if o != nil {
		*opt = *o
	}
}
