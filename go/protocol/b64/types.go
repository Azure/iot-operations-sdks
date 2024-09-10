package b64

import (
	"encoding/base64"
	"encoding/json"
)

// Wrapper for the native Go byte array that will serialize to Base64.
type ByteArray []byte

// MarshalText marshals the byte array to a Base64 string.
func (byteArray ByteArray) MarshalText () ([]byte, error) {
	dst := make([]byte, base64.StdEncoding.EncodedLen(len(byteArray)))
	base64.StdEncoding.Encode(dst, byteArray)
	return dst, nil
}

// MarshalJSON marshals the byte array to a quoted Base64 string.
func (byteArray ByteArray) MarshalJSON () ([]byte, error) {
	bufLen := base64.StdEncoding.EncodedLen(len(byteArray)) + 2
	dst := make([]byte, bufLen)
	base64.StdEncoding.Encode(dst[1:], byteArray)
	dst[0] = '"'
	dst[bufLen - 1] = '"'
	return dst, nil
}

// UnmarshalText unmarshals the byte array from a Base64 string.
func (byteArray *ByteArray) UnmarshalText(b []byte) error {
	*byteArray = make(ByteArray, base64.StdEncoding.DecodedLen(len(b)))
	n, err := base64.StdEncoding.Decode(*byteArray, b)
	if err != nil {
		return err
	}

	*byteArray = (*byteArray)[:n]

	return nil
}

// UnmarshalJSON unmarshals the byte array from a quoted Base64 string.
func (byteArray *ByteArray) UnmarshalJSON(b []byte) error {
	var s string
	var err error
	if err = json.Unmarshal(b, &s); err != nil {
		return err
	}

	*byteArray, err = base64.StdEncoding.DecodeString(s)
	if err != nil {
		return err
	}

	return nil
}
