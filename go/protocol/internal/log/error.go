package log

import (
	"context"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Err logs a protocol error with structured logging.
func (l *Logger) Err(ctx context.Context, err error) {
	l.log(ctx, slog.LevelError, err.Error(), attrs(err))
}

// Additional error attributes for slog.
func attrs(e error) []slog.Attr {
	err, ok := e.(*errors.Error)
	if !ok {
		return nil
	}

	a := make([]slog.Attr, 0, 8)

	a = append(a,
		slog.Int("kind", int(err.Kind)),
		slog.Bool("in_application", err.InApplication),
		slog.Bool("is_shallow", err.IsShallow),
		slog.Bool("is_remote", err.IsRemote),
	)

	httpStatusCode := err.HTTPStatusCode
	if httpStatusCode != 0 {
		a = append(a, slog.Int("http_status_code", httpStatusCode))
	}

	nestedError := err.NestedError
	if nestedError != nil {
		a = append(a, slog.Any("nested_error", nestedError))
	}

	switch err.Kind {
	case errors.HeaderMissing:
		a = append(a, slog.String("header_name", err.HeaderName))
	case errors.HeaderInvalid:
		a = append(a,
			slog.String("header_name", err.HeaderName),
			slog.String("header_value", err.HeaderValue),
		)
	case errors.Timeout:
		a = append(a,
			slog.String("timeout_name", err.TimeoutName),
			slog.Duration("timeout_value", err.TimeoutValue),
		)
	case errors.ConfigurationInvalid, errors.ArgumentInvalid:
		a = append(a,
			slog.String("property_name", err.PropertyName),
			slog.Any("property_value", err.PropertyValue),
		)
	case errors.StateInvalid:
		a = append(a, slog.String("property_name", err.PropertyName))
		if err.PropertyValue != nil {
			a = append(a, slog.Any("property_value", err.PropertyValue))
		}
	case errors.InternalLogicError:
		a = append(a, slog.String("property_name", err.PropertyName))
	case errors.InvocationException:
		if err.PropertyName != "" {
			a = append(a, slog.String("property_name", err.PropertyName))
		}
		if err.PropertyValue != nil {
			a = append(a, slog.Any("property_value", err.PropertyValue))
		}
	case errors.ExecutionException:
		if err.PropertyName != "" {
			a = append(a, slog.String("property_name", err.PropertyName))
		}
	}

	return a
}
