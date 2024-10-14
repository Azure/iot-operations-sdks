// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "log/slog"

// Attrs returns additional error attributes for slog.
func (e *Error) Attrs() []slog.Attr {
	a := make([]slog.Attr, 0, 8)

	a = append(a,
		slog.Int("kind", int(e.Kind)),
		slog.Bool("in_application", e.InApplication),
		slog.Bool("is_shallow", e.IsShallow),
		slog.Bool("is_remote", e.IsRemote),
	)

	httpStatusCode := e.HTTPStatusCode
	if httpStatusCode != 0 {
		a = append(a, slog.Int("http_status_code", httpStatusCode))
	}

	nestedError := e.NestedError
	if nestedError != nil {
		a = append(a, slog.Any("nested_error", nestedError))
	}

	switch e.Kind {
	case HeaderMissing:
		a = append(a, slog.String("header_name", e.HeaderName))
	case HeaderInvalid:
		a = append(a,
			slog.String("header_name", e.HeaderName),
			slog.String("header_value", e.HeaderValue),
		)
	case Timeout:
		a = append(a,
			slog.String("timeout_name", e.TimeoutName),
			slog.Duration("timeout_value", e.TimeoutValue),
		)
	case ConfigurationInvalid, ArgumentInvalid:
		a = append(a,
			slog.String("property_name", e.PropertyName),
			slog.Any("property_value", e.PropertyValue),
		)
	case StateInvalid:
		a = append(a, slog.String("property_name", e.PropertyName))
		if e.PropertyValue != nil {
			a = append(a, slog.Any("property_value", e.PropertyValue))
		}
	case InternalLogicError:
		a = append(a, slog.String("property_name", e.PropertyName))
	case InvocationException:
		if e.PropertyName != "" {
			a = append(a, slog.String("property_name", e.PropertyName))
		}
		if e.PropertyValue != nil {
			a = append(a, slog.Any("property_value", e.PropertyValue))
		}
	case ExecutionException:
		if e.PropertyName != "" {
			a = append(a, slog.String("property_name", e.PropertyName))
		}
	}

	return a
}
