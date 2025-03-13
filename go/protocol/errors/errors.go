// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "time"

type (
	Kind interface {
		String() string

		// Force only internal implementations.
		kind()
	}

	// Client represents a purely client-side error.
	Client struct {
		Message string
		Kind    Kind
		Nested  error
		Shallow bool
	}

	// Remote represents an error that is sent between services over the wire.
	Remote struct {
		Message string
		Kind    Kind
	}

	Timeout struct {
		TimeoutName  string
		TimeoutValue time.Duration
	}

	Cancellation struct{}

	ConfigurationInvalid struct {
		PropertyName  string
		PropertyValue any
	}

	MqttError struct{}

	HeaderMissing struct {
		HeaderName string
	}

	HeaderInvalid struct {
		HeaderName  string
		HeaderValue string
	}

	PayloadInvalid struct{}

	StateInvalid struct {
		PropertyName string
	}

	InternalLogicError struct {
		// Deprecated: Only for wire protocol compat.
		PropertyName string
	}

	UnknownError struct {
		// Deprecated: Only for schemaregistry compat.
		PropertyName string
		// Deprecated: Only for schemaregistry compat.
		PropertyValue any
	}

	ExecutionError struct{}

	UnsupportedVersion struct {
		ProtocolVersion                string
		SupportedMajorProtocolVersions []int
	}
)

func (e *Client) Error() string {
	return e.Message
}

func (e *Client) Unwrap() error {
	return e.Nested
}

func (e *Remote) Error() string {
	return e.Message
}

// IsKind is a shorthand to check if an error is of kind K.
func IsKind[K Kind](err error) (K, bool) {
	switch e := err.(type) {
	case *Client:
		k, ok := e.Kind.(K)
		return k, ok
	case *Remote:
		k, ok := e.Kind.(K)
		return k, ok
	default:
		var k K
		return k, false
	}
}

func (Timeout) kind()              {}
func (Cancellation) kind()         {}
func (ConfigurationInvalid) kind() {}
func (MqttError) kind()            {}
func (HeaderMissing) kind()        {}
func (HeaderInvalid) kind()        {}
func (PayloadInvalid) kind()       {}
func (StateInvalid) kind()         {}
func (InternalLogicError) kind()   {}
func (UnknownError) kind()         {}
func (ExecutionError) kind()       {}
func (UnsupportedVersion) kind()   {}

func (Timeout) String() string              { return "timeout" }
func (Cancellation) String() string         { return "cancellation" }
func (ConfigurationInvalid) String() string { return "invalid configuration" }
func (MqttError) String() string            { return "mqtt error" }
func (HeaderMissing) String() string        { return "missing header" }
func (HeaderInvalid) String() string        { return "invalid header" }
func (PayloadInvalid) String() string       { return "invalid payload" }
func (StateInvalid) String() string         { return "invalid state" }
func (InternalLogicError) String() string   { return "internal logic error" }
func (UnknownError) String() string         { return "unknown error" }
func (ExecutionError) String() string       { return "execution error" }
func (UnsupportedVersion) String() string   { return "unsupported version" }
