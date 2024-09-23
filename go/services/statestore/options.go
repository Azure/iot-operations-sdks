package statestore

import (
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/hlc"
)

type (
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

	// WithTimeout adds a timeout to the request (with second precision).
	WithTimeout time.Duration

	// Extract the underlying invoke options where applicable.
	invokeOptions interface {
		invoke() *protocol.InvokeOptions
	}
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
