// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retry

import (
	"context"
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
)

type logger struct{ log.Logger }

func (l *logger) attempt(
	ctx context.Context,
	task string,
	attempt uint64,
) {
	l.Log(ctx, slog.LevelInfo, "retry",
		slog.String("task", task),
		slog.Uint64("attempt", attempt),
	)
}

func (l *logger) complete(
	ctx context.Context,
	task string,
	attempt uint64,
	err error,
) {
	if err != nil {
		l.Log(ctx, slog.LevelInfo, "retry failed",
			slog.String("task", task),
			slog.Uint64("attempt", attempt),
			slog.String("error", err.Error()),
		)
	} else {
		l.Log(ctx, slog.LevelInfo, "retry succeeded",
			slog.String("task", task),
			slog.Uint64("attempt", attempt),
		)
	}
}
