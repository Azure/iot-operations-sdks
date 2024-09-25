package errors

import (
	"errors"
	"fmt"
)

type (
	// Response errors indicate an error returned from the state store.
	Response string

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
	ErrResponse = errors.New("error response")
	ErrPayload  = errors.New("malformed payload")
	ErrArgument = errors.New("invalid argument")
)

func (e Response) Error() string {
	return fmt.Sprintf("%s: %s", ErrResponse, string(e))
}

func (Response) Unwrap() error {
	return ErrResponse
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
