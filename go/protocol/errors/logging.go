// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package errors

import (
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/version"
)

func (e *Client) Attrs() []slog.Attr {
	a := make([]slog.Attr, 0, 5)

	a = append(a, slog.String("kind", e.Kind.String()))

	if attrs, ok := e.Kind.(log.Attrs); ok {
		a = append(a, attrs.Attrs()...)
	}

	if e.Shallow {
		a = append(a, slog.Bool("shallow", e.Shallow))
	}

	if e.Nested != nil {
		a = append(a, slog.Any("nested", e.Nested))
	}

	return a
}

func (e *Remote) Attrs() []slog.Attr {
	a := make([]slog.Attr, 0, 4)

	a = append(a, slog.String("kind", e.Kind.String()))

	if attrs, ok := e.Kind.(log.Attrs); ok {
		a = append(a, attrs.Attrs()...)
	}

	return append(a, slog.Bool("remote", true))
}

func (e Timeout) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("timeout_name", e.TimeoutName),
		slog.Duration("timeout_value", e.TimeoutValue),
	}
}

func (e ConfigurationInvalid) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("property_name", e.PropertyName),
		slog.Any("property_value", e.PropertyValue),
	}
}

func (e HeaderMissing) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("header_name", e.HeaderName),
	}
}

func (e HeaderInvalid) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("header_name", e.HeaderName),
		slog.String("header_value", e.HeaderValue),
	}
}

func (e StateInvalid) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("property_name", e.PropertyName),
	}
}

func (e UnsupportedVersion) Attrs() []slog.Attr {
	return []slog.Attr{
		slog.String("protocol_version", e.ProtocolVersion),
		slog.String(
			"supported_major_protocol_versions",
			version.SerializeSupported(e.SupportedMajorProtocolVersions),
		),
	}
}
