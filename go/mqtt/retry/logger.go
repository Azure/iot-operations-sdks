// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package retry

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/log"
)

type logger struct{ log.Logger }

func (l *logger) attempt(
	ctx context.Context,
	task string,
	attempt uint64,
) {
	l.Log(ctx, slog.LevelInfo, task,
		slog.Uint64("attempt", attempt),
	)
}

func (l *logger) retry(
	ctx context.Context,
	task string,
	attempt uint64,
	err error,
	interval time.Duration,
) {
	l.Log(ctx, slog.LevelWarn, fmt.Sprintf("%s retrying", task),
		slog.Uint64("attempt", attempt),
		slog.String("error", err.Error()),
		slog.Duration("after", interval),
	)
}

func (l *logger) complete(
	ctx context.Context,
	task string,
	attempt uint64,
	err error,
) {
	if err != nil {
		l.Log(ctx, slog.LevelWarn, fmt.Sprintf("%s failed", task),
			slog.Uint64("attempt", attempt),
			slog.String("error", err.Error()),
		)
	} else {
		l.Log(ctx, slog.LevelInfo, fmt.Sprintf("%s succeeded", task),
			slog.Uint64("attempt", attempt),
		)
	}
}
