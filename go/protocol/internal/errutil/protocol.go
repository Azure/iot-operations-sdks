// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errutil

import (
	"fmt"
	"strconv"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
	"github.com/sosodev/duration"
)

type result struct {
	status            int
	message           string
	application       bool
	name              string
	value             any
	version           string
	supportedVersions []int
}

func ToUserProp(err error) map[string]string {
	if err == nil {
		return (&result{status: 200}).props()
	}

	var message string
	var kind errors.Kind
	switch e := err.(type) {
	case *errors.Client:
		message = e.Message
		kind = e.Kind
	case *errors.Remote:
		message = e.Message
		kind = e.Kind
	default:
		return (&result{
			status:  500,
			message: "invalid error",
		}).props()
	}

	switch k := kind.(type) {
	case errors.HeaderMissing:
		return (&result{
			status:  400,
			message: message,
			name:    k.HeaderName,
		}).props()
	case errors.HeaderInvalid:
		if k.HeaderName == constants.ContentType ||
			k.HeaderName == constants.FormatIndicator {
			return (&result{
				status:  415,
				message: message,
				name:    k.HeaderName,
				value:   k.HeaderValue,
			}).props()
		}
		return (&result{
			status:  400,
			message: message,
			name:    k.HeaderName,
			value:   k.HeaderValue,
		}).props()
	case errors.PayloadInvalid:
		return (&result{
			status:  400,
			message: message,
		}).props()
	case errors.Timeout:
		return (&result{
			status:  408,
			message: message,
			name:    k.TimeoutName,
			value:   duration.Format(k.TimeoutValue),
		}).props()
	case errors.StateInvalid:
		return (&result{
			status:  503,
			message: message,
			name:    k.PropertyName,
		}).props()
	case errors.InternalLogicError:
		return (&result{
			status:  500,
			message: message,
			//nolint:staticcheck // Capture for wire protocol compat.
			name: k.PropertyName,
		}).props()
	case errors.UnknownError:
		return (&result{
			status:  500,
			message: message,
		}).props()
	case errors.ExecutionError:
		return (&result{
			status:      500,
			message:     message,
			application: true,
		}).props()
	case errors.UnsupportedVersion:
		return (&result{
			status:            505,
			message:           message,
			version:           k.ProtocolVersion,
			supportedVersions: k.SupportedMajorProtocolVersions,
		}).props()
	default:
		return (&result{
			status:  500,
			message: "invalid error kind",
			name:    "Kind",
		}).props()
	}
}

func FromUserProp(user map[string]string) error {
	status := user[constants.Status]
	statusMessage := user[constants.StatusMessage]
	propertyName := user[constants.InvalidPropertyName]
	propertyValue := user[constants.InvalidPropertyValue]
	protocolVersion := user[constants.RequestProtocolVersion]
	supportedVersions := user[constants.SupportedProtocolMajorVersion]

	if status == "" {
		return &errors.Client{
			Message: "status missing",
			Kind: errors.HeaderMissing{
				HeaderName: constants.Status,
			},
		}
	}

	code, err := strconv.ParseInt(status, 10, 32)
	if err != nil {
		return &errors.Client{
			Message: "status is not a valid integer",
			Kind: errors.HeaderInvalid{
				HeaderName:  constants.Status,
				HeaderValue: status,
			},
			Nested: err,
		}
	}

	// No error, we're done.
	if code < 400 {
		return nil
	}

	e := &errors.Remote{Message: statusMessage}

	switch code {
	case 400, 415:
		switch {
		case propertyName == "" && propertyValue == "":
			e.Kind = errors.PayloadInvalid{}
		case propertyValue == "":
			e.Kind = errors.HeaderMissing{
				HeaderName: propertyName,
			}
		default:
			e.Kind = errors.HeaderInvalid{
				HeaderName:  propertyName,
				HeaderValue: propertyValue,
			}
		}
	case 408:
		to, err := duration.Parse(propertyValue)
		if err != nil {
			return &errors.Client{
				Message: "invalid timeout value",
				Kind: errors.HeaderInvalid{
					HeaderName:  constants.InvalidPropertyValue,
					HeaderValue: propertyValue,
				},
				Nested: err,
			}
		}
		e.Kind = errors.Timeout{
			TimeoutName:  propertyName,
			TimeoutValue: to.ToTimeDuration(),
		}
	case 500:
		appErr := user[constants.IsApplicationError]
		switch {
		case appErr != "" && appErr != "false":
			e.Kind = errors.ExecutionError{}
		case propertyName != "":
			e.Kind = errors.InternalLogicError{PropertyName: propertyName}
		default:
			e.Kind = errors.UnknownError{}
		}
	case 503:
		e.Kind = errors.StateInvalid{
			PropertyName: propertyName,
		}
	case 505:
		e.Kind = errors.UnsupportedVersion{
			ProtocolVersion: protocolVersion,
			SupportedMajorProtocolVersions: version.ParseSupported(
				supportedVersions,
			),
		}
	default:
		// Treat unknown status as an unknown error, but otherwise allow them.
		k := errors.UnknownError{}

		//nolint:staticcheck // Capture 422 data for schemaregistry.
		k.PropertyName = propertyName
		if propertyValue != "" {
			//nolint:staticcheck // Capture 422 data for schemaregistry.
			k.PropertyValue = propertyValue
		}

		e.Kind = k
	}

	return e
}

func (r *result) props() map[string]string {
	props := make(map[string]string, 5)

	props[constants.Status] = fmt.Sprint(r.status)

	props[constants.StatusMessage] = r.message
	if r.application {
		props[constants.IsApplicationError] = "true"
	}

	if r.name != "" {
		props[constants.InvalidPropertyName] = r.name
		if r.value != nil {
			props[constants.InvalidPropertyValue] = fmt.Sprint(r.value)
		}
	}

	if r.version != "" {
		props[constants.RequestProtocolVersion] = r.version
		props[constants.SupportedProtocolMajorVersion] = version.SerializeSupported(
			r.supportedVersions,
		)
	}

	return props
}
