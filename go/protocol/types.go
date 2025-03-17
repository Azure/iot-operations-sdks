// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// MqttClient is the client used for the underlying MQTT connection.
	MqttClient interface {
		ID() string
		Publish(
			context.Context,
			string,
			[]byte,
			...mqtt.PublishOption,
		) (*mqtt.Ack, error)
		RegisterMessageHandler(mqtt.MessageHandler) func()
		Subscribe(
			context.Context,
			string,
			...mqtt.SubscribeOption,
		) (*mqtt.Ack, error)
		Unsubscribe(
			context.Context,
			string,
			...mqtt.UnsubscribeOption,
		) (*mqtt.Ack, error)
	}

	// Message contains common message data that is exposed to message handlers.
	Message[T any] struct {
		// The message payload.
		Payload T

		// The ID of the calling MQTT client.
		ClientID string

		// The data that identifies a single unique request.
		CorrelationData string

		// The timestamp of when the message was sent.
		Timestamp hlc.HybridLogicalClock

		// All topic tokens resolved from the incoming topic.
		TopicTokens map[string]string

		// Any user-provided metadata values.
		Metadata map[string]string

		// The raw payload data.
		*Data
	}

	// Option represents any of the option types, and can be filtered and
	// applied by the ApplyOptions methods on the option structs.
	Option interface{ option() }
)
