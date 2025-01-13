// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"log/slog"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// Application represents shared application state.
	Application struct {
		hlc *hlc.Shared
		log *slog.Logger
	}

	// ApplicationOption represents a single application option.
	ApplicationOption interface{ application(*ApplicationOptions) }

	// ApplicationOptions are the resolved application options.
	ApplicationOptions struct {
		Logger *slog.Logger
	}
)

// NewApplication creates a new shared application state. Only one of these
// should be created per application.
func NewApplication(opt ...ApplicationOption) (*Application, error) {
	var opts ApplicationOptions
	opts.Apply(opt)

	return &Application{
		hlc: hlc.NewShared(),
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
