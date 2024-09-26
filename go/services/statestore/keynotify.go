package statestore

import (
	"context"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type (
	// KeyNotify represents a registered notification request.
	KeyNotify struct {
		Key string
		C   chan Notify

		done   chan struct{}
		client *Client
		index  int
	}

	// KeyNotifyOption represents a single option for the KeyNotify method.
	KeyNotifyOption interface{ keynotify(*KeyNotifyOptions) }

	// KeyNotifyOptions are the resolved options for the KeyNotify method.
	KeyNotifyOptions struct {
		Timeout time.Duration
	}
)

// KeyNotify requests a notification channel for a key, starting notifications
// if necessary. It returns an object with the channel, which can be used to
// stop this notification request.
func (c *Client) KeyNotify(
	ctx context.Context,
	key string,
	opt ...KeyNotifyOption,
) (*KeyNotify, error) {
	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	if len(c.notify[key]) == 0 {
		if err := c.keyNotify(ctx, key, true, opt...); err != nil {
			return nil, err
		}
	}

	// Give the channel a buffer of 1 so we can iterate through them quickly.
	kn := &KeyNotify{
		Key:    key,
		C:      make(chan Notify, 1),
		done:   make(chan struct{}),
		client: c,
		index:  len(c.notify[key]),
	}
	c.notify[key] = append(c.notify[key], kn)

	return kn, nil
}

// Stop removes this notification and stops notifications for this key if no
// other notifications are registered.
func (kn *KeyNotify) Stop(ctx context.Context, opt ...KeyNotifyOption) error {
	// Stop needs to be thread-safe with other keys, but not with itself, and we
	// need to close the done channel outside of the lock to guarantee that the
	// notify loop will unblock (and eventually free the lock).
	if kn.index >= 0 {
		close(kn.done)
	}

	c := kn.client
	c.notifyMu.Lock()
	defer c.notifyMu.Unlock()

	if kn.index >= 0 {
		// Order doesn't matter, so remove this index quickly by swapping.
		last := len(c.notify[kn.Key]) - 1
		c.notify[kn.Key][kn.index] = c.notify[kn.Key][last]
		c.notify[kn.Key][kn.index].index = kn.index
		c.notify[kn.Key] = c.notify[kn.Key][:last]

		kn.index = -1
		close(kn.C)
	}

	if len(c.notify[kn.Key]) == 0 {
		delete(c.notify, kn.Key)
		if err := c.keyNotify(ctx, kn.Key, false, opt...); err != nil {
			return err
		}
	}

	return nil
}

// KEYNOTIFY invoke shorthand.
func (c *Client) keyNotify(
	ctx context.Context,
	key string,
	run bool,
	opt ...KeyNotifyOption,
) error {
	var args []string
	if run {
		args = []string{"KEYNOTIFY", key}
	} else {
		args = []string{"KEYNOTIFY", key, "STOP"}
	}

	var opts KeyNotifyOptions
	opts.Apply(opt)
	_, err := invoke(ctx, c.invoker, parseOK, &opts, args...)
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
