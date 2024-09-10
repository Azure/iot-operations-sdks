package b64_test

import (
	"encoding/json"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol/b64"
	"github.com/stretchr/testify/require"
)

func TestTypes(t *testing.T) {
	someBytes := b64.ByteArray("Hello, I'm a UTF-8 string.")

	b, err := json.Marshal(someBytes)
	require.NoError(t, err)

	var str string
	err = json.Unmarshal(b, &str)
	require.NoError(t, err)

	require.Equal(t, "SGVsbG8sIEknbSBhIFVURi04IHN0cmluZy4=", str)

	var ba b64.ByteArray
	err = json.Unmarshal(b, &ba)
	require.NoError(t, err)

	require.Equal(t, someBytes, ba)
}
