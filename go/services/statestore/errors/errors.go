// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import (
	"errors"
	"fmt"
)

type (
	// Service errors indicate an error returned from the state store.
	Service string

	// Payload errors indicate a malformed or unexpected payload returned from
	// the state store.
	Payload string

	// Argument errors indicate an invalid argument.
	Argument struct {
		Name  string
		Value any
	}
)

var (
	ErrService  = errors.New("service error")
	ErrPayload  = errors.New("malformed payload")
	ErrArgument = errors.New("invalid argument")
)

//nolint:gosec // False positives.
const (
	TimestampSkew            Service = "the request timestamp is too far in the future; ensure that the client and broker system clocks are synchronized"
	MissingFencingToken      Service = "a fencing token is required for this request"
	FencingTokenSkew         Service = "the request fencing token timestamp is too far in the future; ensure that the client and broker system clocks are synchronized"
	FencingTokenLowerVersion Service = "the request fencing token is a lower version than the fencing token protecting the resource"
	QuotaExceeded            Service = "the quota has been exceeded"
	SyntaxError              Service = "syntax error"
	NotAuthorized            Service = "not authorized"
	UnknownCommand           Service = "unknown command"
	WrongNumberOfArguments   Service = "wrong number of arguments"
	TimestampMissing         Service = "missing timestamp"
	TimestampMalformed       Service = "malformed timestamp"
	KeyLengthZero            Service = "the key length is zero"
)

func (e Service) Error() string {
	return fmt.Sprintf("%s: %s", ErrService, string(e))
}

func (Service) Unwrap() error {
	return ErrService
}

func (e Payload) Error() string {
	return fmt.Sprintf("%s: %s", ErrPayload, string(e))
}

func (Payload) Unwrap() error {
	return ErrPayload
}

func (e Argument) Error() string {
	return fmt.Sprintf("%s: %s=%v", ErrArgument, e.Name, e.Value)
}

func (Argument) Unwrap() error {
	return ErrArgument
}
