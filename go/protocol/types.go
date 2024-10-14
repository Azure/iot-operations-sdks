// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// Client is the client used for the underlying MQTT connection.
	Client = mqtt.Client

	// Message contains common message data that is exposed to message handlers.
	Message[T any] struct {
		// The message payload.
		Payload T

		// The ID of the calling MQTT client.
		// TODO: Rename to "source" to align to Cloud Events spec?
		ClientID string

		// The data that identifies a single unique request.
		CorrelationData string

		// The timestamp of when the message was sent.
		Timestamp hlc.HybridLogicalClock

		// All topic tokens resolved from the incoming topic.
		TopicTokens map[string]string

		// Any user-provided metadata values.
		Metadata map[string]string
	}

	// InvocationError represents an error intentionally returned by a handler
	// to indicate incorrect invocation.
	InvocationError struct {
		Message       string
		PropertyName  string
		PropertyValue any
	}

	// Option represents any of the option types, and can be filtered and
	// applied by the ApplyOptions methods on the option structs.
	Option interface{ option() }
)

// Error returns the invocation error as a string.
func (e InvocationError) Error() string {
	return e.Message
}
