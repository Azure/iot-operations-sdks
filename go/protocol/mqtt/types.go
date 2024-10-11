package mqtt

import "context"

type (
	// Client represents the underlying MQTT client utilized by the protocol
	// library.
	Client interface {
		// Register a topic subscription with a message handler on the client.
		// Update must be called on the returned subscription to actually send
		// the subscription to the MQTT broker.
		Register(
			topic string,
			handler MessageHandler,
		) (Subscription, error)

		// Publish sends a publish request to the MQTT broker.
		Publish(
			ctx context.Context,
			topic string,
			payload []byte,
			opts ...PublishOption,
		) error

		// ClientID returns the identifier used by this client. If one is not
		// provided, a random ID must be generated for reconnection purposes.
		ClientID() string
	}

	// Message represents a received message. The client implementation must
	// support manual ack, since acks are managed by the protocol.
	Message struct {
		Topic   string
		Payload []byte
		PublishOptions
		Ack func() error
	}

	// MessageHandler is a user-defined callback function used to handle
	// messages received on the subscribed topic.
	MessageHandler func(context.Context, *Message) error

	// Subscription represents an open subscription.
	Subscription interface {
		// Unsubscribe this subscription.
		Unsubscribe(context.Context, ...UnsubscribeOption) error

		// Update or initialize the actual underlying MQTT subscription.
		Update(context.Context, ...SubscribeOption) error
	}

	// SubscribeOptions are the resolved subscribe options.
	SubscribeOptions struct {
		NoLocal        bool
		QoS            QoS
		Retain         bool
		RetainHandling RetainHandling
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
		PayloadFormat   PayloadFormat
		QoS             QoS
		ResponseTopic   string
		Retain          bool
		UserProperties  map[string]string
	}

	// PublishOption represents a single publish option.
	PublishOption interface{ publish(*PublishOptions) }
)
