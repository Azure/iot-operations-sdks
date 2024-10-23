// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errutil

import (
	"context"
	"fmt"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Translate a github.com/Azure/iot-operations-sdks/go/internal/mqtt ack/err
// return to an SDK error. An actual error indicates a failure in the client
// library, whereas a response with a failure code indicates an issue in the
// MQTT request.
func Mqtt(ctx context.Context, msg string, ack *mqtt.Ack, err error) error {
	if ack != nil {
		if ack.ReasonCode >= 0x80 {
			return &errors.Error{
				Message: fmt.Sprintf(
					"%s error: %s. reason code: 0x%x",
					msg,
					ack.ReasonString,
					ack.ReasonCode,
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

	// An error from the incoming context overrides any returned error.
	if ctxErr := Context(ctx, msg); ctxErr != nil {
		return ctxErr
	}
	return Normalize(err, msg)
}
