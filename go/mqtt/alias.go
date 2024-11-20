// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "github.com/Azure/iot-operations-sdks/go/internal/mqtt"

// As the implementation of the shared interface, all of its types are aliased
// for convenience.

type (
	// Message represents a received message.
	Message = mqtt.Message
	// MessageHandler is a user-defined callback function used to handle
	// messages received on the subscribed topic.
	MessageHandler = mqtt.MessageHandler
	// ConnectEvent contains the relevent metadata provided to the handler when
	// the MQTT client connects to the broker.
	ConnectEvent = mqtt.ConnectEvent
	// ConnectEventHandler is a user-defined callback function used to respond
	// to connection notifications from the MQTT client.
	ConnectEventHandler = mqtt.ConnectEventHandler
	// DisconnectEvent contains the relevent metadata provided to the handler
	// when the MQTT client disconnects from the broker.
	DisconnectEvent = mqtt.DisconnectEvent
	// DisconnectEventHandler is a user-defined callback function used to
	// respond to disconnection notifications from the MQTT client.
	DisconnectEventHandler = mqtt.DisconnectEventHandler
	// Ack contains values from PUBACK/SUBACK/UNSUBACK packets received from the
	// MQTT server.
	Ack = mqtt.Ack

	// SubscribeOptions are the resolved subscribe options.
	SubscribeOptions = mqtt.SubscribeOptions
	// SubscribeOption represents a single subscribe option.
	SubscribeOption = mqtt.SubscribeOption
	// UnsubscribeOptions are the resolve unsubscribe options.
	UnsubscribeOptions = mqtt.UnsubscribeOptions
	// UnsubscribeOption represents a single unsubscribe option.
	UnsubscribeOption = mqtt.UnsubscribeOption
	// PublishOptions are the resolved publish options.
	PublishOptions = mqtt.PublishOptions
	// PublishOption represents a single publish option.
	PublishOption = mqtt.PublishOption

	// WithContentType sets the content type for the publish.
	WithContentType = mqtt.WithContentType
	// WithCorrelationData sets the correlation data for the publish.
	WithCorrelationData = mqtt.WithCorrelationData
	// WithMessageExpiry sets the message expiry interval for the publish.
	WithMessageExpiry = mqtt.WithMessageExpiry
	// WithNoLocal sets the no local flag for the subscription.
	WithNoLocal = mqtt.WithNoLocal
	// WithPayloadFormat sets the payload format indicator for the publish.
	WithPayloadFormat = mqtt.WithPayloadFormat
	// WithQoS sets the QoS level for the publish or subscribe.
	WithQoS = mqtt.WithQoS
	// WithResponseTopic sets the response topic for the publish.
	WithResponseTopic = mqtt.WithResponseTopic
	// WithRetain sets the retain flag for the publish or the retain-as-publish
	// flag for the subscribe.
	WithRetain = mqtt.WithRetain
	// WithRetainHandling specifies the handling of retained messages on the
	// subscribe.
	WithRetainHandling = mqtt.WithRetainHandling
	// WithUserProperties sets the user properties for the publish or subscribe.
	WithUserProperties = mqtt.WithUserProperties
)
