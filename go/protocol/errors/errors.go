// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "time"

type Kind int

// common fields for both client-side and remote errors.
type BaseError struct {
	Message string
	Kind    Kind

	PropertyName  string
	PropertyValue string
	NestedError   error

	TimeoutName  string
	TimeoutValue time.Duration

	HeaderName  string
	HeaderValue string
}

// purely client-side errors that are never sent over the wire.
type ClientError struct {
	BaseError
	IsShallow bool
}

// errors that can be sent between services over the wire.
type RemoteError struct {
	BaseError
	HTTPStatusCode                 int
	ProtocolVersion                string
	SupportedMajorProtocolVersions []int
	InApplication                  bool
}

// client side.
const (
	Timeout Kind = iota
	Cancellation
	ConfigurationInvalid
	ArgumentInvalid
	MqttError
)

// remote.
const (
	HeaderMissing Kind = iota + 100
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

func (e *ClientError) Error() string {
	return e.Message
}

func (e *RemoteError) Error() string {
	return e.Message
}
