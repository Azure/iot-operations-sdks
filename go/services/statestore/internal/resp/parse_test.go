package resp_test

import (
	"testing"

	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal"
	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal/resp"
	"github.com/stretchr/testify/require"
)

func TestParseError(t *testing.T) {
	_, err := resp.ParseString("SET", []byte("-ERR syntax error\r\n"))
	require.Equal(t, &internal.Error{
		Operation: "SET",
		Message:   "ERR syntax error",
	}, err)
}

func TestParseString(t *testing.T) {
	str, err := resp.ParseString("SET", []byte("+OK\r\n"))
	require.NoError(t, err)
	require.Equal(t, "OK", str)
}

func TestParseNumber(t *testing.T) {
	num, err := resp.ParseNumber("DEL", []byte(":1\r\n"))
	require.NoError(t, err)
	require.Equal(t, 1, num)
}

func TestParseBlob(t *testing.T) {
	blob, err := resp.ParseBlob("GET", []byte("$-1\r\n"))
	require.NoError(t, err)
	require.Nil(t, blob)

	blob, err = resp.ParseBlob("GET", []byte("$4\r\n1234\r\n"))
	require.NoError(t, err)
	require.Equal(t, []byte("1234"), blob)
}

func TestParseBlobArray(t *testing.T) {
	ary, err := resp.ParseBlobArray(
		"NOTIFY",
		[]byte(
			"*4\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n$3\r\nabc\r\n",
		),
	)
	require.NoError(t, err)
	require.Equal(t, [][]byte{
		[]byte("NOTIFY"),
		[]byte("SET"),
		[]byte("VALUE"),
		[]byte("abc"),
	}, ary)
}
