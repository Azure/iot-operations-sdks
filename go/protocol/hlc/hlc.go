// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package hlc

import (
	"fmt"
	"math"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/options"
	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/google/uuid"
)

type (
	// HybridLogicalClock provides a combination of physical and logical clocks
	// used to track timestamps across a distributed system.
	HybridLogicalClock struct {
		timestamp time.Time
		counter   uint64
		nodeID    string
		opt       *HybridLogicalClockOptions
	}

	// Global provides a shared instance of an HLC. Only one of these should
	// typically be created per application.
	Global struct {
		hlc HybridLogicalClock
		mu  sync.Mutex
		opt HybridLogicalClockOptions
	}

	// HybridLogicalClockOption represents a single HLC option.
	HybridLogicalClockOption interface {
		hlc(*HybridLogicalClockOptions)
	}

	// HybridLogicalClockOptions are the resolved HLC options.
	HybridLogicalClockOptions struct {
		MaxClockDrift time.Duration
	}

	// WithMaxClockDrift specifies how long HLCs are allowed to drift from the
	// wall clock before they are considered no longer valid.
	WithMaxClockDrift time.Duration
)

// New creates a new shared instance of an HLC. Only one of these should
// typically be created per application.
func New(opt ...HybridLogicalClockOption) *Global {
	g := &Global{}
	g.opt.Apply(opt)

	if g.opt.MaxClockDrift == 0 {
		g.opt.MaxClockDrift = time.Minute
	}

	g.hlc = HybridLogicalClock{
		timestamp: now(),
		nodeID:    uuid.Must(uuid.NewV7()).String(),
		opt:       &g.opt,
	}

	return g
}

// Get syncs the shared HLC instance to the current time and returns it.
func (g *Global) Get() (HybridLogicalClock, error) {
	g.mu.Lock()
	defer g.mu.Unlock()

	var err error
	g.hlc, err = g.hlc.Update(HybridLogicalClock{})
	if err != nil {
		return HybridLogicalClock{}, err
	}

	return g.hlc, nil
}

// Set syncs the shared HLC instance to the given HLC.
func (g *Global) Set(hlc HybridLogicalClock) error {
	g.mu.Lock()
	defer g.mu.Unlock()

	var err error
	g.hlc, err = g.hlc.Update(hlc)
	return err
}

// UTC returns the physical clock component of the HTC in UTC.
func (hlc HybridLogicalClock) UTC() time.Time {
	// This is always set to UTC, so no need to normalize.
	return hlc.timestamp
}

// Update an HLC based on another one and return the new value.
func (hlc HybridLogicalClock) Update(
	other HybridLogicalClock,
) (HybridLogicalClock, error) {
	// Don't update from the same node.
	if other.nodeID == hlc.nodeID {
		return hlc, nil
	}

	wall := now()

	// Validate both timestamps prior to updating; this guarantees that neither
	// will cause an integer overflow, and because the later timestamp will
	// always be chosen by the update, it also preemptively verifies the final
	// clock skew.
	if err := hlc.validate(wall, hlc.opt); err != nil {
		return HybridLogicalClock{}, err
	}
	if err := other.validate(wall, hlc.opt); err != nil {
		return HybridLogicalClock{}, err
	}

	// Note: The order of checks ensures that a zeroed other HLC behaves as if
	// it were the same as the wall clock.
	updated := HybridLogicalClock{
		nodeID: hlc.nodeID,
		opt:    hlc.opt,
	}
	switch {
	case wall.After(hlc.timestamp) && wall.After(other.timestamp):
		updated.timestamp = wall
		updated.counter = 0

	case hlc.timestamp.Equal(other.timestamp):
		updated.timestamp = hlc.timestamp
		updated.counter = max(hlc.counter, other.counter) + 1

	case hlc.timestamp.After(other.timestamp):
		updated.timestamp = hlc.timestamp
		updated.counter = hlc.counter + 1

	default:
		updated.timestamp = other.timestamp
		updated.counter = other.counter + 1
	}

	return updated, nil
}

// Compare this HLC value with another one.
func (hlc HybridLogicalClock) Compare(other HybridLogicalClock) int {
	if hlc.timestamp.Equal(other.timestamp) {
		switch {
		case hlc.counter > other.counter:
			return 1
		case hlc.counter < other.counter:
			return -1
		default:
			return strings.Compare(hlc.nodeID, other.nodeID)
		}
	}
	return hlc.timestamp.Compare(other.timestamp)
}

// IsZero returns whether this HLC matches its zero value.
func (hlc HybridLogicalClock) IsZero() bool {
	// Only check the timestamp, since if it's a zero time the other values are
	// not meaningful.
	return hlc.timestamp.IsZero()
}

// String retrieves a serialized form of the HLC.
func (hlc HybridLogicalClock) String() string {
	return fmt.Sprintf(
		"%015d:%05d:%s",
		hlc.timestamp.UnixMilli(),
		hlc.counter,
		hlc.nodeID,
	)
}

func (hlc *HybridLogicalClock) validate(
	wall time.Time,
	opt *HybridLogicalClockOptions,
) error {
	switch {
	case hlc.counter == math.MaxUint64:
		return &errors.Error{
			Message:      "integer overflow in HLC counter",
			Kind:         errors.InternalLogicError,
			PropertyName: "Counter",
		}

	case hlc.timestamp.Sub(wall) > opt.MaxClockDrift:
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
func (g *Global) Parse(name, value string) (HybridLogicalClock, error) {
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
		timestamp: time.UnixMilli(timestamp).UTC(),
		counter:   count,
		nodeID:    parts[2],
		opt:       &g.opt,
	}, nil
}

// Apply resolves the provided list of options.
func (o *HybridLogicalClockOptions) Apply(
	opts []HybridLogicalClockOption,
	rest ...HybridLogicalClockOption,
) {
	for opt := range options.Apply[HybridLogicalClockOption](opts, rest...) {
		opt.hlc(o)
	}
}

func (o *HybridLogicalClockOptions) hlc(opt *HybridLogicalClockOptions) {
	if o != nil {
		*opt = *o
	}
}

func (o WithMaxClockDrift) hlc(opt *HybridLogicalClockOptions) {
	opt.MaxClockDrift = time.Duration(o)
}
