// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package caching

import (
	"bytes"
	"strings"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/container"
)

type (
	entry struct {
		req *mqtt.Message
		*result
		start    time.Time // Time the cache entry was requested.
		reqTTL   time.Time // Time the initial request expires.
		cacheTTL time.Time // Time the cache entry fully expires.
	}

	result struct {
		cb   Callback  // sync.OnceValues used to compute and store the result.
		end  time.Time // Time processing completed.
		refs int       // Count of additional references to this result.
		size int       // Recorded size of the result for trimming.
	}

	// For deduplication, the correlation-data is the primary key, but we also
	// add topic to allow enforcement of security policies. For reuse, the key
	// just needs to be unique, so we just reuse the same type for convenience.
	key struct {
		c string
		t string
	}

	Cache struct {
		clock Clock
		ttl   time.Duration
		bytes int

		// TODO: Temporary workaround to pass protocol tests pending equivalency
		// discussions.
		ignoreClient bool

		timeStore container.PriorityMap[key, *entry, int64]
		costStore container.PriorityMap[key, *entry, float64]

		mu sync.Mutex
	}

	Callback = func() (*mqtt.Message, error)

	// Clock used for test dependency injection.
	Clock interface {
		Now() time.Time
	}
)

const (
	// TODO: Verify where these values come from?
	FixedProcessingOverheadMs = 10
	FixedStorageOverheadBytes = 100
	MaxEntryCount             = 10000
	MaxAggregatePayloadBytes  = 10000000
)

// New creates a new cache.
func New(clock Clock, ttl time.Duration, requestTopic string) *Cache {
	return &Cache{
		clock: clock,
		ttl:   ttl,

		ignoreClient: !strings.Contains(requestTopic, "{executorId}"),

		timeStore: container.NewPriorityMap[key, *entry, int64](),
		costStore: container.NewPriorityMap[key, *entry, float64](),
	}
}

// Exec will return the cached response message, executing the provided function
// to produce it if necessary. A nil message with no error indicates that the
// request should be dropped, e.g. if it has expired or a duplicate request is
// already in-flight.
func (c *Cache) Exec(req *mqtt.Message, cb Callback) (*mqtt.Message, error) {
	e := c.get(req, cb)
	if e == nil {
		return nil, nil
	}
	return e.cb()
}

// Get or create the cache entry. This is separate from exec so that we don't
// retain the cache mutex while the callback is executing.
func (c *Cache) get(req *mqtt.Message, cb Callback) *entry {
	c.mu.Lock()
	defer c.mu.Unlock()

	id := getKey(req)
	now := c.clock.Now().UTC()

	if cached, ok := c.timeStore.Get(id); ok {
		if cached.end.IsZero() || now.After(cached.reqTTL) {
			return nil
		}
		// TODO: Check equivalency?
		return cached
	}

	e := &entry{
		req:    req,
		start:  now,
		reqTTL: now.Add(time.Duration(req.MessageExpiry) * time.Second),
	}

	// The cache entry has a TTL equal to its request until after processing,
	// after which it may be updated to reflect equivalent-request caching.
	e.cacheTTL = e.reqTTL
	c.timeStore.Set(id, e, e.cacheTTL.UnixNano())

	// Attempt to find an equivalent request to use its existing result.
	if equiv, ok := c.costStore.Find(func(cached *entry) bool {
		return c.equivalentRequest(req, cached.req) &&
			now.Before(cached.end.Add(c.ttl))
	}); ok {
		e.result = equiv.result
		e.refs++
	} else {
		e.result = &result{
			cb: sync.OnceValues(func() (*mqtt.Message, error) {
				res, err := cb()
				return c.set(e, res, err, c.clock.Now().UTC())
			}),
		}
	}

	return e
}

// Store the result in the cache.
func (c *Cache) set(
	e *entry,
	res *mqtt.Message,
	err error,
	now time.Time,
) (*mqtt.Message, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	id := getKey(e.req)
	e.end = now

	// Don't perform equivalent-request caching for errors.
	if c.ttl > 0 && res != nil {
		// Update the TTL if it is longer than the request TTL.
		if e.end.Add(c.ttl).After(e.cacheTTL) {
			e.cacheTTL = e.end.Add(c.ttl)
			c.timeStore.Set(id, e, e.cacheTTL.UnixNano())
		}

		// Add the entry to the cost store.
		c.costStore.Set(id, e, costWeightedBenefit(res, e.end.Sub(e.start)))
	} else {
		// If the request has already expired, don't bother sending a response.
		if e.end.After(e.cacheTTL) {
			c.timeStore.Delete(id)
			return nil, nil
		}

		// Don't keep the request message around if we don't need it.
		e.req = nil
	}

	if res != nil {
		e.size = sizeOf(res)
		c.bytes += e.size
	}

	c.trim(now)

	return res, err
}

// Trim the entries in the cache based on expiry and cost-weighted benefit.
func (c *Cache) trim(now time.Time) {
	// First, remove all entries that have expired.
	for {
		id, e, ok := c.timeStore.Next()
		if !ok || now.Before(e.cacheTTL) {
			break
		}
		c.remove(id, e)
	}

	for c.timeStore.Len() >= MaxEntryCount || c.bytes >= MaxAggregatePayloadBytes {
		id, e, ok := c.costStore.Next()
		if !ok {
			break
		}

		// If the request has expired, fully remove it. Otherwise, remove it
		// from the cost store and update its TTL to be only its request expiry,
		// since we're no longer equivalent-request caching it.
		if now.After(e.reqTTL) {
			c.remove(id, e)
		} else {
			e.req = nil
			e.cacheTTL = e.reqTTL
			c.timeStore.Set(id, e, e.cacheTTL.UnixNano())
			c.costStore.Delete(id)
		}
	}
}

// Fully remove the cache from both stores and dereference its data.
func (c *Cache) remove(id key, e *entry) {
	c.timeStore.Delete(id)
	c.costStore.Delete(id)
	e.refs--
	if e.refs < 0 {
		c.bytes -= e.size
	}
}

func sizeOf(res *mqtt.Message) int {
	return len(res.Payload) // TODO: Include more values?
}

func costWeightedBenefit(msg *mqtt.Message, exec time.Duration) float64 {
	executionBypassBenefit := FixedProcessingOverheadMs + exec.Milliseconds()
	storageCost := FixedStorageOverheadBytes + sizeOf(msg)
	return float64(executionBypassBenefit) / float64(storageCost)
}

func getKey(msg *mqtt.Message) key {
	return key{string(msg.CorrelationData), msg.Topic}
}

func (c *Cache) equivalentRequest(req, cached *mqtt.Message) bool {
	// This means we got a "duplicate" request that somehow missed the cache.
	// Don't treat it as equivalent.
	if bytes.Equal(req.CorrelationData, cached.CorrelationData) {
		return false
	}

	if len(req.UserProperties) != len(cached.UserProperties) {
		return false
	}

	if req.Topic != cached.Topic {
		return false
	}

	// TODO: This does a byte-for-byte comparison, which lets us avoid parsing
	// on a cache hit but won't match on cases that are actually equivalent
	// (like a difference in key ordering). Do we instead want to check equality
	// on the parsed value?
	if !bytes.Equal(req.Payload, cached.Payload) {
		return false
	}

	for k, v := range req.UserProperties {
		if c.ignoreMetadata(k) {
			continue
		}
		if v != cached.UserProperties[k] {
			return false
		}
	}

	return true
}

// Ignore ephemeral properties and other internal properties are not directly
// set by the user for purposes of determining equivalency.
func (c *Cache) ignoreMetadata(key string) bool {
	switch key {
	case constants.Timestamp, constants.Partition:
		return true
	case constants.SourceID:
		return c.ignoreClient
	default:
		return false
	}
}
