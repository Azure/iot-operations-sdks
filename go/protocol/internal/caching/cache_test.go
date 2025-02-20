// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package caching

import (
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
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

func TestDuplicateCacheProcessing(t *testing.T) {
	clock := fixedClock(time.Now())
	c := New(&clock)

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

func (tc *test) messages() (req, res *mqtt.Message) {
	opts := mqtt.PublishOptions{
		CorrelationData: []byte{1, 2, 3, 4, tc.num},
		MessageExpiry:   uint32(tc.exp.Seconds()),
		UserProperties: map[string]string{
			constants.SourceID: "client",
		},
	}
	req = &mqtt.Message{Payload: []byte(tc.req), PublishOptions: opts}
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
