// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "time"

type (
	// Error represents a structured protocol error.
	Error struct {
		Message string
		Kind    Kind

		NestedError error

		HeaderName  string
		HeaderValue string

		TimeoutName  string
		TimeoutValue time.Duration

		PropertyName  string
		PropertyValue any

		ProtocolVersion                string
		SupportedMajorProtocolVersions []int

		// The following will be set automatically by the library and should not
		// be updated manually.

		InApplication  bool
		IsShallow      bool
		IsRemote       bool
		HTTPStatusCode int
	}

	// Kind defines the type of error being thrown.
	Kind int
)

// The following are the defined error kinds.
const (
	HeaderMissing Kind = iota
	HeaderInvalid
	PayloadInvalid
	Timeout
	Cancellation
	ConfigurationInvalid
	ArgumentInvalid
	StateInvalid
	InternalLogicError
	UnknownError
	InvocationException
	ExecutionException
	MqttError
	UnsupportedRequestVersion
	UnsupportedResponseVersion
)

// Error returns the error as a string.
func (e *Error) Error() string {
	return e.Message
}
