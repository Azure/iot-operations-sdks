// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import "log/slog"

// client errors.
func (e *Client) Attrs() []slog.Attr {
	a := baseAttrs(&e.Base)

	if e.IsShallow {
		a = append(a, slog.Bool("is_shallow", e.IsShallow))
	}

	return a
}

// remote errors.
func (e *Remote) Attrs() []slog.Attr {
	a := baseAttrs(&e.Base)

	if e.HTTPStatusCode != 0 {
		a = append(a, slog.Int("http_status_code", e.HTTPStatusCode))
	}

	if e.InApplication {
		a = append(a, slog.Bool("in_application", e.InApplication))
	}

	if e.ProtocolVersion != "" {
		a = append(a, slog.String("protocol_version", e.ProtocolVersion))
	}

	if len(e.SupportedMajorProtocolVersions) > 0 {
		a = append(
			a,
			slog.Any(
				"supported_major_protocol_versions",
				e.SupportedMajorProtocolVersions,
			),
		)
	}

	return a
}

// get attributes from Base.
func baseAttrs(e *Base) []slog.Attr {
	a := make([]slog.Attr, 0, 8)

	a = append(a, slog.Int("kind", int(e.Kind)))

	if e.NestedError != nil {
		a = append(a, slog.Any("nested_error", e.NestedError))
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
