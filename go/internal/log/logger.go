// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package log

import (
	"context"
	"log/slog"
	"runtime"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
)

type (
	// Logger is a wrapper around an slog.Logger with additional helpers and nil
	// checking.
	Logger struct{ Wrapped *slog.Logger }

	// Attrs represents an object that exposes extra slog attributes to log.
	Attrs interface {
		Attrs() []slog.Attr
	}
)

// Wrap the slog logger.
func Wrap(logger *slog.Logger) Logger {
	return Logger{logger}
}

// Log is designed to build logging wrappers; it should not be called directly.
// See: https://pkg.go.dev/log/slog#hdr-Wrapping_output_methods
func (l Logger) Log(
	ctx context.Context,
	level slog.Level,
	msg string,
	attrs ...slog.Attr,
) {
	if !l.Enabled(ctx, level) {
		return
	}

	now := wallclock.Instance.Now()
	var pcs [1]uintptr
	runtime.Callers(3, pcs[:])

	r := slog.NewRecord(now, level, msg, pcs[0])
	r.AddAttrs(attrs...)
	_ = l.Wrapped.Handler().Handle(ctx, r)
}

// Err logs a error with structured logging.
func (l Logger) Err(ctx context.Context, err error) {
	if a, ok := err.(Attrs); ok {
		l.Log(ctx, slog.LevelError, err.Error(), a.Attrs()...)
	} else {
		l.Log(ctx, slog.LevelError, err.Error())
	}
}

// Enabled indicates that the logger is enabled for the given logging level.
func (l Logger) Enabled(ctx context.Context, level slog.Level) bool {
	return l.Wrapped != nil && l.Wrapped.Enabled(ctx, level)
}
