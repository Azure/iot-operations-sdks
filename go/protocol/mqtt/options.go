package mqtt

type (
	// WithContentType sets the content type for the publish.
	WithContentType string

	// WithCorrelationData sets the correlation data for the publish.
	WithCorrelationData []byte

	// WithMessageExpiry sets the message expiry interval for the publish.
	WithMessageExpiry uint32

	// WithNoLocal sets the no local flag for the subscription.
	WithNoLocal bool

	// WithPayloadFormat sets the payload format indicator for the publish.
	WithPayloadFormat PayloadFormat

	// WithQoS sets the QoS level for the publish or subscribe.
	WithQoS QoS

	// WithResponseTopic sets the response topic for the publish.
	WithResponseTopic string

	// WithRetain sets the retain flag for the publish or the retain-as-publish
	// flag for the subscribe.
	WithRetain bool

	// WithRetainHandling specifies the handling of retained messages on this
	// subscribe.
	WithRetainHandling RetainHandling

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
	opt.PayloadFormat = PayloadFormat(o)
}

func (o WithQoS) publish(opt *PublishOptions) {
	opt.QoS = QoS(o)
}

func (o WithQoS) subscribe(opt *SubscribeOptions) {
	opt.QoS = QoS(o)
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
	opt.RetainHandling = RetainHandling(o)
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
	for _, opt := range opts {
		if opt != nil {
			opt.subscribe(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.subscribe(o)
		}
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
	for _, opt := range opts {
		if opt != nil {
			opt.unsubscribe(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.unsubscribe(o)
		}
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
	for _, opt := range opts {
		if opt != nil {
			opt.publish(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.publish(o)
		}
	}
}

// Assign non-nil options.
func (o *PublishOptions) publish(opt *PublishOptions) {
	if o != nil {
		*opt = *o
	}
}
