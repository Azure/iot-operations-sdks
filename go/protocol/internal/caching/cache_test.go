package caching

import (
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/stretchr/testify/require"
)

type (
	fixedClock time.Time

	test struct {
		num byte
		req string
		res string
		err error
		exp time.Duration
		exe time.Duration
	}
)

func TestEquivalentCacheEviction(t *testing.T) {
	clock := fixedClock(time.Now())
	c := New(&clock, time.Hour, "")

	msg1 := &test{1, "req1", "res1", nil, time.Minute, time.Second}
	msg2 := &test{2, "req2", "res2", nil, time.Minute, 10 * time.Second}
	eqv1 := &test{3, "req1", "res1", nil, time.Minute, time.Second}
	eqv2 := &test{4, "req2", "res2", nil, time.Minute, time.Second}

	// Artificially fill the cache.
	c.bytes = MaxAggregatePayloadBytes - len(msg1.res) - len(msg2.res)/2

	// Initial request does not hit the cache.
	msg1.testCacheHit(t, &clock, c, false)

	// Advance the clock so that the first request expires but does not timeout.
	clock.Add(2 * time.Minute)

	// First request is expired, and should return nothing.
	msg1.testCacheRes(t, &clock, c, true, "", nil)

	// Add a second request with higher storage benefit that overflows the
	// cache. This should evict the first request.
	msg2.testCacheHit(t, &clock, c, false)

	// An equivalent request to the longer execution should be cached.
	eqv2.testCacheHit(t, &clock, c, true)

	// The faster request should have been evicted.
	eqv1.testCacheHit(t, &clock, c, false)
}

func TestDuplicateCacheEviction(t *testing.T) {
	clock := fixedClock(time.Now())
	c := New(&clock, time.Hour, "")

	msg1 := &test{1, "req1", "res1", nil, time.Minute, time.Second}
	msg2 := &test{2, "req2", "res2", nil, time.Minute, 10 * time.Second}
	eqv1 := &test{3, "req1", "res1", nil, time.Minute, time.Second}

	// Artificially fill the cache.
	c.bytes = MaxAggregatePayloadBytes - len(msg1.res) - len(msg2.res)/2

	// Initial request does not hit the cache.
	msg1.testCacheHit(t, &clock, c, false)

	// Add a second request with higher storage benefit that overflows the
	// cache. This should evict the first request from equivalency caching but
	// not from duplicate caching.
	msg2.testCacheHit(t, &clock, c, false)

	// A duplicate request still hits the cache.
	msg1.testCacheHit(t, &clock, c, true)

	// An equivalent request does not.
	eqv1.testCacheHit(t, &clock, c, false)
}

func TestDuplicateCacheProcessing(t *testing.T) {
	clock := fixedClock(time.Now())
	c := New(&clock, time.Hour, "")

	msg1 := &test{1, "req1", "res1", nil, time.Minute, time.Second}

	lock := make(chan struct{})

	// Perform the initial cache in the background with controlled execution.
	go func() {
		req, res := msg1.messages()
		_, _ = c.Exec(req, func() (*mqtt.Message, error) {
			lock <- struct{}{}
			<-lock
			return res, nil
		})
		lock <- struct{}{}
	}()

	<-lock

	// An in-flight request should return nothing, to avoid duplicate processing
	// and responses.
	msg1.testCacheRes(t, &clock, c, true, "", nil)

	lock <- struct{}{}
	<-lock

	// After the response completes, the cache is hit successfully.
	msg1.testCacheHit(t, &clock, c, true)
}

func (tc *test) messages() (*mqtt.Message, *mqtt.Message) {
	opts := mqtt.PublishOptions{
		CorrelationData: []byte{1, 2, 3, 4, tc.num},
		MessageExpiry:   uint32(tc.exp.Seconds()),
		UserProperties: map[string]string{
			constants.InvokerClientID: "client",
		},
	}
	req := &mqtt.Message{Payload: []byte(tc.req), PublishOptions: opts}
	var res *mqtt.Message
	if tc.err == nil {
		res = &mqtt.Message{Payload: []byte(tc.res), PublishOptions: opts}
	}
	return req, res
}

func (tc *test) cache(
	clock *fixedClock,
	c *Cache,
) (bool, *mqtt.Message, error) {
	hit := true
	req, res := tc.messages()
	msg, err := c.Exec(req, func() (*mqtt.Message, error) {
		hit = false
		clock.Add(tc.exe)
		return res, tc.err
	})
	return hit, msg, err
}

func (tc *test) testCacheHit(
	t *testing.T,
	clock *fixedClock,
	c *Cache,
	expHit bool,
) {
	tc.testCacheRes(t, clock, c, expHit, tc.res, tc.err)
}

func (tc *test) testCacheRes(
	t *testing.T,
	clock *fixedClock,
	c *Cache,
	expHit bool,
	expRes string,
	expErr error,
) {
	hit, res, err := tc.cache(clock, c)
	require.Equal(t, expHit, hit)
	if expRes != "" {
		require.NotNil(t, res)
		require.Equal(t, tc.res, string(res.Payload))
	} else {
		require.Nil(t, res)
	}
	require.Equal(t, expErr, err)
}

func (c *fixedClock) Now() time.Time {
	return time.Time(*c)
}

func (c *fixedClock) Add(d time.Duration) {
	*c = fixedClock(time.Time(*c).Add(d))
}
