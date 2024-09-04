package iso_test

import (
	"encoding/json"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol/iso"
	"github.com/stretchr/testify/require"
)

type (
	Types struct {
		Date     iso.Date
		DateTime iso.DateTime
		Duration iso.Duration
		Time     iso.Time
	}

	Strings struct {
		Date     string
		DateTime string
		Duration string
		Time     string
	}
)

func TestTypes(t *testing.T) {
	utc := time.Unix(2e9, 0).UTC()
	d := time.Minute + time.Second

	types := Types{
		Date:     iso.Date(utc),
		DateTime: iso.DateTime(utc),
		Duration: iso.Duration(d),
		Time:     iso.Time(utc),
	}

	b, err := json.Marshal(types)
	require.NoError(t, err)

	var str Strings
	err = json.Unmarshal(b, &str)
	require.NoError(t, err)

	require.Equal(t, "2033-05-18", str.Date)
	require.Equal(t, "2033-05-18T03:33:20Z", str.DateTime)
	require.Equal(t, "PT1M1S", str.Duration)
	require.Equal(t, "03:33:20Z", str.Time)

	var typ Types
	err = json.Unmarshal(b, &typ)
	require.NoError(t, err)

	dateOnly := time.Date(2033, 5, 18, 0, 0, 0, 0, time.UTC)
	timeOnly := time.Date(1, 1, 1, 3, 33, 20, 0, time.UTC)

	require.Equal(t, dateOnly, time.Time(typ.Date))
	require.Equal(t, utc, time.Time(typ.DateTime))
	require.Equal(t, d, time.Duration(typ.Duration))
	require.Equal(t, timeOnly, time.Time(typ.Time))
}
