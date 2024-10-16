// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"encoding/json"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

type (
	// Encoding is a translation between a concrete Go type T and byte data.
	// All methods *must* be thread-safe.
	Encoding[T any] interface {
		ContentType() string
		PayloadFormat() byte
		Serialize(T) ([]byte, error)
		Deserialize([]byte) (T, error)
	}

	// JSON is a simple implementation of a JSON encoding.
	JSON[T any] struct{}

	// Empty represents an encoding that contains no value.
	Empty struct{}

	// Raw represents no encoding.
	Raw struct{}
)

// Utility to serialize with a protocol error.
func serialize[T any](encoding Encoding[T], value T) ([]byte, error) {
	bytes, err := encoding.Serialize(value)
	if err != nil {
		if e, ok := err.(*errors.Error); ok {
			return nil, e
		}
		return nil, &errors.Error{
			Message: "cannot serialize payload",
			Kind:    errors.PayloadInvalid,
		}
	}
	return bytes, nil
}

// Utility to deserialize with a protocol error.
func deserialize[T any](encoding Encoding[T], bytes []byte) (T, error) {
	value, err := encoding.Deserialize(bytes)
	if err != nil {
		if e, ok := err.(*errors.Error); ok {
			return value, e
		}
		return value, &errors.Error{
			Message: "cannot deserialize payload",
			Kind:    errors.PayloadInvalid,
		}
	}
	return value, nil
}

// ContentType returns the JSON MIME type.
func (JSON[T]) ContentType() string {
	return "application/json"
}

// PayloadFormat indicates that JSON is valid UTF8.
func (JSON[T]) PayloadFormat() byte {
	return 1
}

// Serialize translates the Go type T into JSON bytes.
func (JSON[T]) Serialize(t T) ([]byte, error) {
	return json.Marshal(t)
}

// Deserialize translates JSON bytes into the Go type T.
func (JSON[T]) Deserialize(data []byte) (T, error) {
	var t T
	err := json.Unmarshal(data, &t)
	return t, err
}

// ContentType returns the empty MIME type.
func (Empty) ContentType() string {
	return ""
}

// PayloadFormat indicates that empty is not (meaningfully) valid UTF8.
func (Empty) PayloadFormat() byte {
	return 0
}

// Serialize validates that the payload is empty.
func (Empty) Serialize(t any) ([]byte, error) {
	if t != nil {
		return nil, &errors.Error{
			Message: "unexpected payload for empty type",
			Kind:    errors.PayloadInvalid,
		}
	}
	return nil, nil
}

// Deserialize validates that the payload is empty.
func (Empty) Deserialize(data []byte) (any, error) {
	if len(data) != 0 {
		return nil, &errors.Error{
			Message: "unexpected payload for empty type",
			Kind:    errors.PayloadInvalid,
		}
	}
	return nil, nil
}

// ContentType returns the raw MIME type.
func (Raw) ContentType() string {
	return "application/octet-stream"
}

// PayloadFormat indicates that raw is not known to be valid UTF8.
func (Raw) PayloadFormat() byte {
	return 0
}

// Serialize returns the bytes unchanged.
func (Raw) Serialize(t []byte) ([]byte, error) {
	return t, nil
}

// Deserialize returns the bytes unchanged.
func (Raw) Deserialize(data []byte) ([]byte, error) {
	return data, nil
}
