package caching

import (
	"encoding/json"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
	"github.com/stretchr/testify/require"
)

func TestIdempotentCache(t *testing.T) {
	c := New(true, "")

	// Add 1st entry for idempotent request.
	byt, err := json.Marshal("helloworld")
	require.NoError(t, err)

	c.bytes = MaxAggregatePayloadBytes - len(byt) - len(byt)/2

	req1 := message("foo", "fooCorrData1", "request1")
	c.Set(
		req1,
		&mqtt.Message{Payload: byt},
		wallclock.Instance.Now().Add(time.Minute),
		wallclock.Instance.Now().Add(5*time.Second),
		time.Second,
	)
	res, ok := c.Get(req1)
	require.True(t, ok)

	var st string
	err = json.Unmarshal(res.Payload, &st)
	require.NoError(t, err)
	require.Equal(t, st, "helloworld")

	// Add 2nd entry for idempotent request with higher exec benefit.
	byt, err = json.Marshal("helloworld2")
	require.NoError(t, err)

	req2 := message("foo", "fooCorrData2", "request2")
	c.Set(
		req2,
		&mqtt.Message{Payload: byt},
		wallclock.Instance.Now().Add(time.Minute),
		wallclock.Instance.Now().Add(5*time.Second),
		5*time.Second,
	)
	res, ok = c.Get(req2)
	require.True(t, ok)

	err = json.Unmarshal(res.Payload, &st)
	require.NoError(t, err)
	require.Equal(t, st, "helloworld2")

	// Verify if the entry with least exec benefit is not in the cache anymore.
	_, ok = c.Get(req1)
	require.False(t, ok)
}

func TestNonIdempotentCache(t *testing.T) {
	c := New(false, "")

	byt, err := json.Marshal("helloworld")
	require.NoError(t, err)

	c.bytes = MaxAggregatePayloadBytes - len(byt) - len(byt)/2

	// Add 1st entry.
	req1 := message("foo", "fooCorrData1", "request1")
	c.Set(
		req1,
		&mqtt.Message{Payload: byt},
		wallclock.Instance.Now().Add(time.Minute),
		wallclock.Instance.Now().Add(5*time.Second),
		time.Second,
	)
	res, ok := c.Get(req1)
	require.True(t, ok)

	var st string
	err = json.Unmarshal(res.Payload, &st)
	require.NoError(t, err)
	require.Equal(t, st, "helloworld")

	// Add 2nd entry.
	req2 := message("foo", "fooCorrData2", "request2")
	byt, err = json.Marshal("helloworld2")
	require.NoError(t, err)
	c.Set(
		req2,
		&mqtt.Message{Payload: byt},
		wallclock.Instance.Now().Add(time.Minute),
		wallclock.Instance.Now().Add(5*time.Second),
		5*time.Second,
	)
	res, ok = c.Get(req2)
	require.True(t, ok)

	err = json.Unmarshal(res.Payload, &st)
	require.NoError(t, err)
	require.Equal(t, st, "helloworld2")

	// Verify 1st entry still exists.
	res, ok = c.Get(req1)
	require.True(t, ok)

	err = json.Unmarshal(res.Payload, &st)
	require.NoError(t, err)
	require.Equal(t, st, "helloworld")
}

func message(clientID, correlationID, payload string) *mqtt.Message {
	return &mqtt.Message{
		Payload: []byte(payload),
		PublishOptions: mqtt.PublishOptions{
			CorrelationData: []byte(correlationID),
			UserProperties: map[string]string{
				constants.InvokerClientID: clientID,
			},
		},
	}
}
