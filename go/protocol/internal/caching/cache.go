// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package caching

import (
	"sync"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/container"
)

type (
	entry struct {
		res *mqtt.Message
		err error
		ttl time.Time
	}

	// The correlation-data is the primary key, but we also add response topic
	// to allow enforcement of security policies.
	key struct {
		c string
		t string
	}

	Cache struct {
		clock Clock
		store container.PriorityMap[key, *entry, int64]
		mu    sync.Mutex
	}

	// Clock used for test dependency injection.
	Clock interface {
		Now() time.Time
	}
)

// New creates a new cache.
func New(clock Clock) *Cache {
	return &Cache{
		clock: clock,
		store: container.NewPriorityMap[key, *entry, int64](),
	}
}

// Exec will return the cached response message, executing the provided function
// to produce it if necessary. A nil message with no error indicates that the
// request should be dropped, e.g. if it has expired or a duplicate request is
// already in-flight.
func (c *Cache) Exec(
	req *mqtt.Message,
	cb func() (*mqtt.Message, error),
) (*mqtt.Message, error) {
	e, res, err := c.get(req)
	if e != nil {
		res, err := cb()
		return c.set(e, res, err)
	}
	return res, err
}

// Get or create the cache entry. This is separate from exec so that we don't
// retain the cache mutex while the callback is executing. Note that the entry,
// if returned, must only be modified under the lock.
func (c *Cache) get(req *mqtt.Message) (*entry, *mqtt.Message, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	id := key{string(req.CorrelationData), req.ResponseTopic}
	now := c.clock.Now().UTC()

	if e, ok := c.store.Get(id); ok {
		if now.After(e.ttl) {
			return nil, nil, nil
		}
		return nil, e.res, e.err
	}

	e := &entry{
		ttl: now.Add(time.Duration(req.MessageExpiry) * time.Second),
	}

	c.store.Set(id, e, e.ttl.UnixNano())
	return e, nil, nil
}

// Store the result in the cache.
func (c *Cache) set(
	e *entry,
	res *mqtt.Message,
	err error,
) (*mqtt.Message, error) {
	c.mu.Lock()
	defer c.mu.Unlock()

	now := c.clock.Now().UTC()

	// Trim the cache.
	for {
		id, e, ok := c.store.Next()
		if !ok || now.Before(e.ttl) {
			break
		}
		c.store.Delete(id)
	}

	// If the request has already expired, don't bother sending a response.
	if now.After(e.ttl) {
		return nil, nil
	}

	e.res = res
	e.err = err
	return res, err
}
