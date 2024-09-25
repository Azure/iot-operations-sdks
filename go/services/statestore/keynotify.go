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
		Timeout time.Duration
	}
)

// KeyNotify requests notification for a key. It returns a callback to remove
// the provided notification handler.
func (c *Client) KeyNotify(
	ctx context.Context,
	key string,
	cb func(context.Context, *Notify),
	opt ...KeyNotifyOption,
) (func(context.Context, ...KeyNotifyOption) error, error) {
	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	if len(c.notify[key]) == 0 {
		if err := c.keyNotify(ctx, key, true, opt); err != nil {
			return nil, err
		}
	}

	h := &notify{cb, len(c.notify[key])}
	c.notify[key] = append(c.notify[key], h)

	return func(sCtx context.Context, sOpt ...KeyNotifyOption) error {
		c.notifyMu.Lock()
		defer c.notifyMu.Unlock()

		if h.index >= 0 {
			// Order doesn't matter, so remove this index quickly by swapping.
			last := len(c.notify[key]) - 1
			c.notify[key][h.index] = c.notify[key][last]
			c.notify[key][h.index].index = h.index
			c.notify[key] = c.notify[key][:last]
			h.index = -1
		}

		if len(c.notify[key]) == 0 {
			delete(c.notify, key)
			if err := c.keyNotify(sCtx, key, false, opt, sOpt...); err != nil {
				return err
			}
		}

		return nil
	}, nil
}

// KEYNOTIFY invoke shorthand.
func (c *Client) keyNotify(
	ctx context.Context,
	key string,
	run bool,
	opts []KeyNotifyOption,
	rest ...KeyNotifyOption,
) error {
	var args []string
	if run {
		args = []string{"KEYNOTIFY", key}
	} else {
		args = []string{"KEYNOTIFY", key, "STOP"}
	}

	var o KeyNotifyOptions
	o.Apply(opts, rest...)
	_, err := invoke(ctx, c.invoker, parseOK, &o, args...)
	return err
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

func (o WithTimeout) keynotify(opt *KeyNotifyOptions) {
	opt.Timeout = time.Duration(o)
}

func (o *KeyNotifyOptions) invoke() *protocol.InvokeOptions {
	return &protocol.InvokeOptions{
		MessageExpiry: uint32(o.Timeout.Seconds()),
	}
}
