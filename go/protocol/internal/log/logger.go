package log

import (
	"context"
	"log/slog"
	"runtime"

	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
)

// Logger is a wrapper around an slog.Logger with additional helpers and nil
// checking.
// TODO: Add helpers for additional logging levels as needed. Each should call
// the log helper method, not the underlying logger directly.
type Logger struct{ logger *slog.Logger }

// Wrap the slog logger.
func Wrap(logger *slog.Logger) Logger {
	return Logger{logger}
}

// https://pkg.go.dev/log/slog#hdr-Wrapping_output_methods
func (l *Logger) log(
	ctx context.Context,
	level slog.Level,
	msg string,
	attrs []slog.Attr,
) {
	if l.logger == nil || !l.logger.Enabled(ctx, level) {
		return
	}

	now := wallclock.Instance.Now()
	var pcs [1]uintptr
	runtime.Callers(3, pcs[:])

	r := slog.NewRecord(now, level, msg, pcs[0])
	r.AddAttrs(attrs...)
	_ = l.logger.Handler().Handle(ctx, r)
}
