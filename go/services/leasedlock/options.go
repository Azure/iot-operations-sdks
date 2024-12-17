// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package leasedlock

import (
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

type (
	// Option represents a single option for the lock requests.
	Option interface{ request(*Options) }

	// Options are the resolved options for the lock requests.
	Options struct {
		Timeout   time.Duration
		SessionID string
	}

	// WithTimeout adds a timeout to the request (with second precision).
	WithTimeout time.Duration

	// WithSessionID adds an optional session ID suffix to the lock holder to
	// allow distinct locks on the same key with the same MQTT client.
	WithSessionID string
)

// Apply resolves the provided list of options.
func (o *Options) Apply(
	opts []Option,
	rest ...Option,
) {
	for opt := range options.Apply[Option](opts, rest...) {
		opt.request(o)
	}
}

func (o *Options) request(opt *Options) {
	if o != nil {
		*opt = *o
	}
}

func (o WithTimeout) request(opt *Options) {
	opt.Timeout = time.Duration(o)
}

func (o WithSessionID) request(opt *Options) {
	opt.SessionID = string(o)
}

func (o *Options) del() *statestore.DelOptions {
	return &statestore.DelOptions{
		Timeout: o.Timeout,
	}
}

func (o *Options) get() *statestore.GetOptions {
	return &statestore.GetOptions{
		Timeout: o.Timeout,
	}
}

func (o *Options) keynotify() *statestore.KeyNotifyOptions {
	return &statestore.KeyNotifyOptions{
		Timeout: o.Timeout,
	}
}

func (o *Options) set() *statestore.SetOptions {
	return &statestore.SetOptions{
		Timeout: o.Timeout,
	}
}

func (o *Options) vdel() *statestore.VDelOptions {
	return &statestore.VDelOptions{
		Timeout: o.Timeout,
	}
}
