package hlc

import (
	"fmt"
	"math"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/google/uuid"
)

// HybridLogicalClock provides a combination of physical and logical clocks used
// to track timestamps across a distributed system.
type HybridLogicalClock struct {
	Timestamp time.Time
	Counter   uint64
	NodeID    string
}

const maxClockDrift = time.Minute

var (
	instance = HybridLogicalClock{
		Timestamp: now(),
		NodeID:    uuid.Must(uuid.NewV7()).String(),
	}
	instanceMux sync.Mutex
)

// Get syncs the global HLC instance to the current time and returns it.
func Get() (HybridLogicalClock, error) {
	instanceMux.Lock()
	defer instanceMux.Unlock()

	var err error
	instance, err = instance.Update(HybridLogicalClock{})
	if err != nil {
		return HybridLogicalClock{}, err
	}

	return instance, nil
}

// Set syncs the global HLC instance to the given HLC.
func Set(hlc HybridLogicalClock) error {
	instanceMux.Lock()
	defer instanceMux.Unlock()

	var err error
	instance, err = instance.Update(hlc)
	return err
}

// Update an HLC based on another one and return the new value.
func (hlc HybridLogicalClock) Update(
	other HybridLogicalClock,
) (HybridLogicalClock, error) {
	// Don't update from the same node.
	if other.NodeID == hlc.NodeID {
		return hlc, nil
	}

	wall := now()

	// Validate both timestamps prior to updating; this guarantees that neither
	// will cause an integer overflow, and because the later timestamp will
	// always be chosen by the update, it also preemptively verifies the final
	// clock skew.
	if err := hlc.validate(wall); err != nil {
		return HybridLogicalClock{}, err
	}
	if err := other.validate(wall); err != nil {
		return HybridLogicalClock{}, err
	}

	// Note: The order of checks ensures that a zeroed other HLC behaves as if
	// it were the same as the wall clock.
	updated := HybridLogicalClock{NodeID: hlc.NodeID}
	switch {
	case wall.After(hlc.Timestamp) && wall.After(other.Timestamp):
		updated.Timestamp = wall
		updated.Counter = 0

	case hlc.Timestamp.Equal(other.Timestamp):
		updated.Timestamp = hlc.Timestamp
		updated.Counter = max(hlc.Counter, other.Counter) + 1

	case hlc.Timestamp.After(other.Timestamp):
		updated.Timestamp = hlc.Timestamp
		updated.Counter = hlc.Counter + 1

	default:
		updated.Timestamp = other.Timestamp
		updated.Counter = other.Counter + 1
	}

	return updated, nil
}

// Compare this HLC value with another one.
func (hlc HybridLogicalClock) Compare(other HybridLogicalClock) int {
	if hlc.Timestamp.Equal(other.Timestamp) {
		switch {
		case hlc.Counter > other.Counter:
			return 1
		case hlc.Counter < other.Counter:
			return -1
		default:
			return strings.Compare(hlc.NodeID, other.NodeID)
		}
	}
	return hlc.Timestamp.Compare(other.Timestamp)
}

// IsZero returns whether this HLC matches its zero value.
func (hlc HybridLogicalClock) IsZero() bool {
	// Only check the timestamp, since if it's a zero time the other values are
	// not meaningful.
	return hlc.Timestamp.IsZero()
}

// String retrieves a serialized form of the HLC.
func (hlc HybridLogicalClock) String() string {
	return fmt.Sprintf(
		"%015d:%05d:%s",
		hlc.Timestamp.UnixMilli(),
		hlc.Counter,
		hlc.NodeID,
	)
}

func (hlc *HybridLogicalClock) validate(wall time.Time) error {
	switch {
	case hlc.Counter == math.MaxUint64:
		return &errors.Error{
			Message:      "integer overflow in HLC counter",
			Kind:         errors.InternalLogicError,
			PropertyName: "Counter",
		}

	case hlc.Timestamp.Sub(wall) > maxClockDrift:
		return &errors.Error{
			Message:      "clock drift exceeds maximum",
			Kind:         errors.StateInvalid,
			PropertyName: "MaxClockDrift",
		}

	default:
		return nil
	}
}

// Get the current time in UTC with ms precision.
func now() time.Time {
	return wallclock.Instance.Now().UTC().Truncate(time.Millisecond)
}

// Parse the HLC from a string.
func Parse(name, value string) (HybridLogicalClock, error) {
	parts := strings.Split(value, ":")
	if len(parts) != 3 {
		return HybridLogicalClock{}, &errors.Error{
			Message:     "HLC must contain three segments separated by ':'",
			Kind:        errors.HeaderInvalid,
			HeaderName:  name,
			HeaderValue: value,
		}
	}

	timestamp, err := strconv.ParseInt(parts[0], 10, 64)
	if err != nil {
		return HybridLogicalClock{}, &errors.Error{
			Message:     "first HLC segment is not a valid integer",
			Kind:        errors.HeaderInvalid,
			HeaderName:  name,
			HeaderValue: value,
		}
	}

	count, err := strconv.ParseUint(parts[1], 10, 64)
	if err != nil {
		return HybridLogicalClock{}, &errors.Error{
			Message:     "second HLC segment is not a valid integer",
			Kind:        errors.HeaderInvalid,
			HeaderName:  name,
			HeaderValue: value,
		}
	}

	return HybridLogicalClock{
		Timestamp: time.UnixMilli(timestamp),
		Counter:   count,
		NodeID:    parts[2],
	}, nil
}
