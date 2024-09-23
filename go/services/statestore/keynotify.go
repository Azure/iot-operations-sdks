package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type (
	// KeyNotifyOption represents a single option for the KeyNotify method.
	KeyNotifyOption interface{ keynotify(*KeyNotifyOptions) }

	// KeyNotifyOptions are the resolved options for the KeyNotify method.
	KeyNotifyOptions struct {
		Stop    bool
		Timeout time.Duration
	}

	// WithStop indicates that the notification should be stopped.
	WithStop bool
)

// KeyNotify requests or stops notification for a key. If a stop is requested on
// a key that did not have notifications, it will return false with no error.
func (c *Client) KeyNotify(
	ctx context.Context,
	key string,
	opt ...KeyNotifyOption,
) (*Response[bool], error) {
	var opts KeyNotifyOptions
	opts.Apply(opt)

	args := []string{"KEYNOTIFY", key}
	if opts.Stop {
		args = append(args, "STOP")
	}
	return invoke(ctx, c.invoker, parseOK, &opts, args...)
}

// Apply resolves the provided list of options.
func (o *KeyNotifyOptions) Apply(
	opts []KeyNotifyOption,
	rest ...KeyNotifyOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.keynotify(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.keynotify(o)
		}
	}
}

func (o *KeyNotifyOptions) keynotify(opt *KeyNotifyOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithStop) keynotify(opt *KeyNotifyOptions) {
	opt.Stop = bool(o)
}

func (o WithTimeout) keynotify(opt *KeyNotifyOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *KeyNotifyOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
	}
}
