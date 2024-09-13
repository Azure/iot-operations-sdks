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
	status  int
	error   *errors.Error
	name    string
	value   any
	version string
}

func ToUserProp(err error) map[string]string {
	if err == nil {
		return result{status: 200}.props()
	}

	e, ok := err.(*errors.Error)
	if !ok {
		return result{
			status: 500,
			error: &errors.Error{
				Message: "invalid error",
				Kind:    errors.InternalLogicError,
			},
			name: "Error",
		}.props()
	}

	switch e.Kind {
	case errors.HeaderMissing:
		return result{
			status: 400,
			error:  e,
			name:   e.HeaderName,
		}.props()
	case errors.HeaderInvalid:
		if e.HeaderName == constants.ContentType ||
			e.HeaderName == constants.FormatIndicator {
			return result{
				status: 415,
				error:  e,
				name:   e.HeaderName,
				value:  e.HeaderValue,
			}.props()
		}
		return result{
			status: 400,
			error:  e,
			name:   e.HeaderName,
			value:  e.HeaderValue,
		}.props()
	case errors.PayloadInvalid:
		return result{
			status: 400,
			error:  e,
		}.props()
	case errors.Timeout:
		return result{
			status: 408,
			error:  e,
			name:   e.TimeoutName,
			value:  duration.Format(e.TimeoutValue),
		}.props()
	case errors.ArgumentInvalid:
		// Treat "argument invalid" as "execution exception" over the wire.
		// This can happen e.g. for invalid header names.
		return result{
			status: 500,
			error: &errors.Error{
				Message:       e.Message,
				Kind:          errors.ExecutionException,
				InApplication: true,
			},
			name:  e.PropertyName,
			value: e.PropertyValue,
		}.props()
	case errors.StateInvalid:
		return result{
			status: 503,
			error:  e,
			name:   e.PropertyName,
			value:  e.PropertyValue,
		}.props()
	case errors.InternalLogicError:
		return result{
			status: 500,
			error:  e,
			name:   e.PropertyName,
		}.props()
	case errors.UnknownError:
		return result{
			status: 500,
			error:  e,
		}.props()
	case errors.InvocationException:
		return result{
			status: 422,
			error:  e,
			name:   e.PropertyName,
			value:  e.PropertyValue,
		}.props()
	case errors.ExecutionException:
		// Note: The error should always be flagged InApplication in this case.
		return result{
			status: 500,
			error:  e,
			name:   e.PropertyName,
		}.props()
	case errors.UnsupportedRequestVersion:
		return result{
			status:  505,
			error:   e,
			version: e.ProtocolVersion,
		}.props()
	default:
		return result{
			status: 500,
			error: &errors.Error{
				Message: "invalid error kind",
				Kind:    errors.InternalLogicError,
			},
			name: "Kind",
		}.props()
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
		return &errors.Error{
			Message:    "status missing",
			Kind:       errors.HeaderMissing,
			HeaderName: constants.Status,
		}
	}

	code, err := strconv.ParseInt(status, 10, 32)
	if err != nil {
		return &errors.Error{
			Message:     "status is not a valid integer",
			Kind:        errors.HeaderInvalid,
			HeaderName:  constants.Status,
			HeaderValue: status,
		}
	}

	// No error, we're done.
	if code < 400 {
		return nil
	}

	e := &errors.Error{
		Message:        statusMessage,
		IsRemote:       true,
		HTTPStatusCode: int(code),
	}

	appErr := user[constants.IsApplicationError]
	if appErr != "" && appErr != "false" {
		e.InApplication = true
	}

	switch code {
	case 400, 415:
		switch {
		case propertyName == "" && propertyValue == "":
			e.Kind = errors.PayloadInvalid
		case propertyValue == "":
			e.Kind = errors.HeaderMissing
		default:
			e.Kind = errors.HeaderInvalid
		}
		e.HeaderName = propertyName
		e.HeaderValue = propertyValue
	case 408:
		to, err := duration.Parse(propertyValue)
		if err != nil {
			return &errors.Error{
				Message:     "invalid timeout value",
				Kind:        errors.HeaderInvalid,
				HeaderName:  constants.InvalidPropertyValue,
				HeaderValue: propertyValue,
			}
		}
		e.Kind = errors.Timeout
		e.TimeoutName = propertyName
		e.TimeoutValue = to.ToTimeDuration()
	case 422:
		e.Kind = errors.InvocationException
		e.PropertyName = propertyName
		if propertyValue != "" {
			e.PropertyValue = propertyValue
		}
	case 500:
		switch {
		case e.InApplication:
			e.Kind = errors.ExecutionException
		case propertyName == "":
			e.Kind = errors.UnknownError
		default:
			e.Kind = errors.InternalLogicError
		}
		e.PropertyName = propertyName
	case 503:
		e.Kind = errors.StateInvalid
		e.PropertyName = propertyName
		if propertyValue != "" {
			e.PropertyValue = propertyValue
		}
	case 505:
		e.Kind = errors.UnsupportedRequestVersion
		e.ProtocolVersion = protocolVersion
		e.SupportedMajorProtocolVersions = version.ParseSupported(
			supportedVersions,
		)
	default:
		// Treat unknown status as an unknown error, but otherwise allow them.
		e.Kind = errors.UnknownError
	}

	return e
}

func (r result) props() map[string]string {
	props := make(map[string]string, 5)

	props[constants.Status] = fmt.Sprint(r.status)

	if r.error != nil {
		props[constants.StatusMessage] = r.error.Message
		if r.error.InApplication {
			props[constants.IsApplicationError] = "true"
		}
	}

	if r.name != "" {
		props[constants.InvalidPropertyName] = r.name
		if r.value != nil {
			props[constants.InvalidPropertyValue] = fmt.Sprint(r.value)
		}
	}

	if r.version != "" {
		props[constants.RequestProtocolVersion] = r.version
		props[constants.SupportedProtocolMajorVersion] = version.SupportedString
	}

	return props
}
