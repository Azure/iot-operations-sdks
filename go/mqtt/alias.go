// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "github.com/Azure/iot-operations-sdks/go/internal/mqtt"

// As the implementation of the shared interface, all of its types are aliased
// for convenience.
type (
	Message                = mqtt.Message
	MessageHandler         = mqtt.MessageHandler
	ConnectEvent           = mqtt.ConnectEvent
	ConnectEventHandler    = mqtt.ConnectEventHandler
	DisconnectEvent        = mqtt.DisconnectEvent
	DisconnectEventHandler = mqtt.DisconnectEventHandler

	SubscribeOptions   = mqtt.SubscribeOptions
	SubscribeOption    = mqtt.SubscribeOption
	UnsubscribeOptions = mqtt.UnsubscribeOptions
	UnsubscribeOption  = mqtt.UnsubscribeOption
	PublishOptions     = mqtt.PublishOptions
	PublishOption      = mqtt.PublishOption

	WithContentType     = mqtt.WithContentType
	WithCorrelationData = mqtt.WithCorrelationData
	WithMessageExpiry   = mqtt.WithMessageExpiry
	WithNoLocal         = mqtt.WithNoLocal
	WithPayloadFormat   = mqtt.WithPayloadFormat
	WithQoS             = mqtt.WithQoS
	WithResponseTopic   = mqtt.WithResponseTopic
	WithRetain          = mqtt.WithRetain
	WithRetainHandling  = mqtt.WithRetainHandling
	WithUserProperties  = mqtt.WithUserProperties
)
