// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "time"

type (
	Kind int

	// common fields for both client-side and remote errors.
	Base struct {
		Message string
		Kind    Kind

		PropertyName  string
		PropertyValue any
		NestedError   error

		TimeoutName  string
		TimeoutValue time.Duration

		HeaderName  string
		HeaderValue string
	}

	// purely client-side errors that are never sent over the wire.
	Client struct {
		Base
		IsShallow bool
	}

	// errors that can be sent between services over the wire.
	Remote struct {
		Base
		HTTPStatusCode                 int
		ProtocolVersion                string
		SupportedMajorProtocolVersions []int
		InApplication                  bool
	}
)

const (
	Timeout Kind = iota
	Cancellation
	ConfigurationInvalid
	ArgumentInvalid
	MqttError
	HeaderMissing
	HeaderInvalid
	PayloadInvalid
	StateInvalid
	InternalLogicError
	UnknownError
	InvocationException
	ExecutionException
	UnsupportedRequestVersion
	UnsupportedResponseVersion
)

func (e *Client) Error() string {
	return e.Message
}

func (e *Remote) Error() string {
	return e.Message
}
