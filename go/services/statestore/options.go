package statestore

import (
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
	// SetOption represents a single option for the Set method.
	SetOption interface{ set(*SetOptions) }

	// SetOptions are the resolved options for the Set method.
	SetOptions struct {
		Condition    Condition
		Expiry       time.Duration
		FencingToken hlc.HybridLogicalClock
	}

	// Condition specifies the conditions under which the key will be set.
	Condition string

	// WithCondition indicates that the key should only be set under the given
	// conditions.
	WithCondition Condition

	// WithExpiry indicates that the key should expire after the given duration
	// (with millisecond precision).
	WithExpiry time.Duration

	// WithFencingToken adds a fencing token to the set request to provide lock
	// ownership checking.
	WithFencingToken hlc.HybridLogicalClock
)

const (
	// Always indicates that the key should always be set to the provided value.
	// This is the default.
	Always Condition = ""

	// NotExists indicates that the key should only be set if it does not exist.
	NotExists Condition = "NX"

	// NotExistOrEqual indicates that the key should only be set if it does not
	// exist or is equal to the set value. This is typically used to update the
	// expiry on the key.
	NotExistsOrEqual Condition = "NEX"
)

// Apply resolves the provided list of options.
func (o *SetOptions) Apply(
	opts []SetOption,
	rest ...SetOption,
) {
	for _, opt := range opts {
		if opt != nil {
			opt.set(o)
		}
	}
	for _, opt := range rest {
		if opt != nil {
			opt.set(o)
		}
	}
}

func (o *SetOptions) set(opt *SetOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithCondition) set(opt *SetOptions) {
	opt.Condition = Condition(o)
}

func (o WithExpiry) set(opt *SetOptions) {
	opt.Expiry = time.Duration(o)
}

func (o WithFencingToken) set(opt *SetOptions) {
	opt.FencingToken = hlc.HybridLogicalClock(o)
}
