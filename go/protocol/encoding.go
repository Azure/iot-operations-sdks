// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"encoding/json"
	stderr "errors"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
)

type (
	// Encoding is a translation between a concrete Go type T and encoded data.
	// All methods *must* be thread-safe.
	Encoding[T any] interface {
		Serialize(T) (*Data, error)
		Deserialize(*Data) (T, error)
	}

	// Data represents encoded values along with their transmitted content type.
	// It may also be provided as the encoding to act as a passthrough, allowing
	// the calling code to fully specify the data manually.
	Data struct {
		Payload       []byte
		ContentType   string
		PayloadFormat byte
	}

	// JSON is a simple implementation of a JSON encoding.
	JSON[T any] struct{}

	// Empty represents an encoding that contains no value.
	Empty struct{}

	// Raw represents a raw byte stream.
	Raw struct{}
)

// ErrUnsupportedContentType should be returned if the content type is not
// supported by this encoding.
var ErrUnsupportedContentType = stderr.New("unsupported content type")

// Utility to serialize with a protocol error.
func serialize[T any](encoding Encoding[T], value T) (*Data, error) {
	data, err := encoding.Serialize(value)
	if err != nil {
		if e, ok := err.(*errors.Error); ok {
			return nil, e
		}
		return nil, &errors.Error{
			Message: "cannot serialize payload",
			Kind:    errors.PayloadInvalid,
		}
	}
	return data, nil
}

// Utility to deserialize with a protocol error.
func deserialize[T any](encoding Encoding[T], data *Data) (T, error) {
	value, err := encoding.Deserialize(data)
	if err != nil {
		if e, ok := err.(*errors.Error); ok {
			return value, e
		}
		if stderr.Is(err, ErrUnsupportedContentType) {
			return value, &errors.Error{
				Message:     "content type mismatch",
				Kind:        errors.HeaderInvalid,
				HeaderName:  constants.ContentType,
				HeaderValue: data.ContentType,
			}
		}
		return value, &errors.Error{
			Message: "cannot deserialize payload",
			Kind:    errors.PayloadInvalid,
		}
	}
	return value, nil
}

// Serialize translates the Go type T into JSON bytes.
func (JSON[T]) Serialize(t T) (*Data, error) {
	bytes, err := json.Marshal(t)
	if err != nil {
		return nil, err
	}
	return &Data{bytes, "application/json", 1}, nil
}

// Deserialize translates JSON bytes into the Go type T.
func (JSON[T]) Deserialize(data *Data) (T, error) {
	var t T
	switch data.ContentType {
	case "", "application/json":
		err := json.Unmarshal(data.Payload, &t)
		return t, err
	default:
		return t, ErrUnsupportedContentType
	}
}

// Serialize validates that the payload is empty.
func (Empty) Serialize(t any) (*Data, error) {
	if t != nil {
		return nil, &errors.Error{
			Message: "unexpected payload for empty type",
			Kind:    errors.PayloadInvalid,
		}
	}
	return &Data{}, nil
}

// Deserialize validates that the payload is empty.
func (Empty) Deserialize(data *Data) (any, error) {
	if len(data.Payload) != 0 {
		return nil, &errors.Error{
			Message: "unexpected payload for empty type",
			Kind:    errors.PayloadInvalid,
		}
	}
	return nil, nil
}

// Serialize returns the bytes unchanged.
func (Raw) Serialize(t []byte) (*Data, error) {
	return &Data{t, "application/octet-stream", 0}, nil
}

// Deserialize returns the bytes unchanged.
func (Raw) Deserialize(data *Data) ([]byte, error) {
	switch data.ContentType {
	case "", "application/octet-stream":
		return data.Payload, nil
	default:
		return nil, ErrUnsupportedContentType
	}
}

// Serialize returns the data unchanged.
func (Data) Serialize(t *Data) (*Data, error) {
	return t, nil
}

// Deserialize returns the data unchanged.
func (Data) Deserialize(data *Data) (*Data, error) {
	return data, nil
}
