package statestore

import "time"

type (
	// SetOption represents a single option for the Set method.
	SetOption interface{ set(*SetOptions) }

	// SetOptions are the resolved options for the Set method.
	SetOptions struct {
		Condition Condition
		Expiry    time.Duration
	}

	// Condition specifies the conditions under which the key will be set.
	Condition byte

	// WithCondition indicates that the key should only be set under the given
	// conditions.
	WithCondition Condition

	// WithExpiry indicates that the key should expire after the given duration
	// (with millisecond precision).
	WithExpiry time.Duration
)

const (
	// Always indicates that the key should always be set to the provided value.
	// This is the default.
	Always Condition = iota

	// NotExists indicates that the key should only be set if it does not exist.
	NotExists

	// NotExistOrEqual indicates that the key should only be set if it does not
	// exist or is equal to the set value. This is typically used to update the
	// expiry on the key.
	NotExistsOrEqual
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
