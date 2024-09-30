package resp_test

import (
	"errors"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
	"github.com/stretchr/testify/require"
)

func TestParseError(t *testing.T) {
	_, err := resp.ParseString([]byte("-ERR syntax error\r\n"))
	require.Equal(t, "error response: ERR syntax error", err.Error())
	require.True(t, errors.Is(err, statestore.ErrResponse))
}

func TestParseString(t *testing.T) {
	str, err := resp.ParseString([]byte("+OK\r\n"))
	require.NoError(t, err)
	require.Equal(t, "OK", str)
}

func TestParseNumber(t *testing.T) {
	num, err := resp.ParseNumber([]byte(":1\r\n"))
	require.NoError(t, err)
	require.Equal(t, 1, num)
}

func TestParseBlob(t *testing.T) {
	blob, err := resp.ParseBlob([]byte("$-1\r\n"))
	require.NoError(t, err)
	require.Nil(t, blob)

	blob, err = resp.ParseBlob([]byte("$4\r\n1234\r\n"))
	require.NoError(t, err)
	require.Equal(t, []byte("1234"), blob)
}

func TestParseBlobArray(t *testing.T) {
	ary, err := resp.ParseBlobArray([]byte(
		"*4\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n$3\r\nabc\r\n",
	))
	require.NoError(t, err)
	require.Equal(t, [][]byte{
		[]byte("NOTIFY"),
		[]byte("SET"),
		[]byte("VALUE"),
		[]byte("abc"),
	}, ary)
}
