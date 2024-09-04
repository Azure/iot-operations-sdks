package internal

import (
	"context"
	"fmt"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Translation from MQTT errors to SDK errors.
type ErrMap[T any] struct {
	String string
	Reason func(*T) (byte, string)
}

// Translate a MQTT response to an SDK error. An actual error indicates a
// failure in the client library, whereas a response with a failure code
// indicates an issue in the MQTT request.
func (e *ErrMap[T]) Translate(ctx context.Context, res *T, err error) error {
	// An error from the incoming context overrides any returned error.
	if ctxErr := errors.Context(ctx, e.String); ctxErr != nil {
		return ctxErr
	}

	// Paho returns an error for failed MQTT results as well as the result.
	// Since we want those to be returned as MQTT errors, check them first.
	if res != nil {
		if code, reason := e.Reason(res); code >= 0x80 {
			return &errors.Error{
				Message: fmt.Sprintf(
					"%s error: %s. reason code: 0x%x",
					e.String,
					reason,
					code,
				),
				Kind: errors.MqttError,
			}
		}
	} else if err == nil {
		return &errors.Error{
			Message: "the MQTT client returned a nil response without an error",
			Kind:    errors.InternalLogicError,
		}
	}

	return errors.Normalize(err, e.String)
}
