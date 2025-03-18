// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"log/slog"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// Application represents shared application state.
	Application struct {
		hlc *hlc.Global
		log *slog.Logger
	}

	// ApplicationOption represents a single application option.
	ApplicationOption interface{ application(*ApplicationOptions) }

	// ApplicationOptions are the resolved application options.
	ApplicationOptions struct {
		MaxClockDrift time.Duration
		Logger        *slog.Logger
	}

	// WithMaxClockDrift specifies how long HLCs are allowed to drift from the
	// wall clock before they are considered no longer valid.
	WithMaxClockDrift time.Duration
)

// NewApplication creates a new shared application state. Only one of these
// should be created per application.
func NewApplication(opt ...ApplicationOption) (*Application, error) {
	var opts ApplicationOptions
	opts.Apply(opt)

	return &Application{
		hlc: hlc.New(&hlc.HybridLogicalClockOptions{
			MaxClockDrift: opts.MaxClockDrift,
		}),
		log: opts.Logger,
	}, nil
}

// GetHLC syncs the application HLC instance to the current time and returns it.
func (a *Application) GetHLC() (hlc.HybridLogicalClock, error) {
	return a.hlc.Get()
}

// SetHLC syncs the application HLC instance to the given HLC.
func (a *Application) SetHLC(val hlc.HybridLogicalClock) error {
	return a.hlc.Set(val)
}

// Apply resolves the provided list of options.
func (o *ApplicationOptions) Apply(
	opts []ApplicationOption,
	rest ...ApplicationOption,
) {
	for opt := range options.Apply[ApplicationOption](opts, rest...) {
		opt.application(o)
	}
}

func (o *ApplicationOptions) application(opt *ApplicationOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithMaxClockDrift) application(opt *ApplicationOptions) {
	opt.MaxClockDrift = time.Duration(o)
}
