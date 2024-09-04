package caching

import (
	"bytes"
	"strings"
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/container"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
)

type (
	entry struct {
		req *mqtt.Message
		res *mqtt.Message
		exp time.Time
		ttl time.Time
	}

	key struct {
		invokerClientID string
		correlationData string
	}

	Cache struct {
		entries    map[key]*entry
		idempotent bool

		// TODO: Temporary workaround to pass METL tests pending equivalency
		// discussions.
		ignoreClient bool

		costQueue container.PriorityQueue[key, float64]
		timeQueue container.PriorityQueue[key, int64]

		bytes int

		mutex sync.RWMutex
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
func New(idempotent bool, requestTopic string) *Cache {
	return &Cache{
		entries:    map[key]*entry{},
		idempotent: idempotent,

		ignoreClient: !strings.Contains(requestTopic, "{executorId}"),

		costQueue: container.NewPriorityQueue[key, float64](),
		timeQueue: container.NewPriorityQueue[key, int64](),
	}
}

// Get returns the response for a request from the cache if available.
func (c *Cache) Get(req *mqtt.Message) (*mqtt.Message, bool) {
	c.mutex.RLock()
	defer c.mutex.RUnlock()

	now := wallclock.Instance.Now().UTC()

	if e, ok := c.entries[getKey(req)]; ok {
		if now.After(e.exp) {
			return nil, false
		}
		// TODO: Check equivalency?
		return e.res, true
	}

	if c.idempotent {
		// TODO: This is a linear search for simplicity; consider another
		// algorithm for speed.
		for _, e := range c.entries {
			if c.equivalentRequest(req, e.req) {
				if now.After(e.ttl) {
					return nil, false
				}
				return e.res, true
			}
		}
	}

	return nil, false
}

// Set stores the request and response in the cache.
func (c *Cache) Set(
	req, res *mqtt.Message,
	expiry, ttl time.Time,
	exec time.Duration,
) {
	c.mutex.Lock()
	defer c.mutex.Unlock()

	// expiry refers to the time at which the command request expires. This also
	// indicates the time until which the invoker will wait for the response
	// from the server. This time also indicates for how long that response is
	// valid for non-idempotent and duplicate requests.
	//
	// ttl refers to the time until which the response of an equivalent
	// idempotent request is valid.
	//
	// For non-idempotent requests => timeout = expiry
	// For idempotent requests => timeout = max(expiry, ttl)
	timeout := expiry
	if c.idempotent && expiry.Before(ttl) {
		timeout = ttl
	}

	if wallclock.Instance.Now().UTC().Before(timeout) {
		id := getKey(req)

		e := &entry{res: res, exp: expiry}
		c.entries[id] = e
		c.bytes += len(res.Payload) // TODO: Include more values?
		c.timeQueue.Push(id, timeout.UnixNano())

		if c.idempotent {
			e.req = req // TODO: Include in cost?
			e.ttl = ttl
			c.costQueue.Push(id, costWeightedBenefit(res, exec))
		}

		c.trim()
	}
}

// Trim the entries in the cache based on expiry and cost-weighted benefit.
func (c *Cache) trim() {
	// First, remove all entries that are expired.
	now := wallclock.Instance.Now().UTC()
	for {
		id, ok := c.timeQueue.Peek()
		if !ok || now.Before(c.entries[id].exp) {
			break
		}
		c.removeEntry(id)
	}

	// NOTE: This will never evict non-idempotent requests, since they're not in
	// the cost queue.
	for len(c.entries) >= MaxEntryCount || c.bytes >= MaxAggregatePayloadBytes {
		id, ok := c.costQueue.Peek()
		if !ok {
			break
		}
		c.removeEntry(id)
	}
}

func (c *Cache) removeEntry(id key) {
	c.timeQueue.Remove(id)
	c.costQueue.Remove(id)
	c.bytes -= len(c.entries[id].res.Payload)
	delete(c.entries, id)
}

func costWeightedBenefit(msg *mqtt.Message, exec time.Duration) float64 {
	executionBypassBenefit := FixedProcessingOverheadMs + exec.Milliseconds()
	storageCost := FixedStorageOverheadBytes + len(msg.Payload)
	return float64(executionBypassBenefit) / float64(storageCost)
}

func getKey(msg *mqtt.Message) key {
	return key{
		invokerClientID: msg.UserProperties[constants.InvokerClientID],
		correlationData: string(msg.CorrelationData),
	}
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
	case constants.Timestamp, constants.FencingToken, constants.Partition:
		return true
	case constants.InvokerClientID:
		return c.ignoreClient
	default:
		return false
	}
}
