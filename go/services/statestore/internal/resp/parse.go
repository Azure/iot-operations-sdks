package resp

import (
	"bytes"
	"fmt"
	"strconv"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/services/statestore/errors"
)

func PayloadError(format string, args ...any) errors.Payload {
	return errors.Payload(fmt.Sprintf(format, args...))
}

func parseStr(typ byte, data []byte) (arg string, idx int, err error) {
	sep := bytes.Index(data, separator)
	if sep < 0 {
		return "", 0, PayloadError("missing separator")
	}

	arg = string(data[1:sep])
	idx = sep + len(separator)

	switch data[0] {
	case '-':
		return "", 0, errors.Service(strings.TrimPrefix(arg, "ERR "))
	case typ:
		return arg, idx, nil
	default:
		return "", 0, PayloadError("wrong type %q", data[0])
	}
}

func parseNum(typ byte, data []byte) (num, idx int, err error) {
	val, idx, err := parseStr(typ, data)
	if err != nil {
		return 0, 0, err
	}

	num, err = strconv.Atoi(val)
	if err != nil {
		return 0, 0, PayloadError("invalid number %q", val)
	}

	return num, idx, nil
}

func parseBlob[T Bytes](typ byte, data []byte) (blob T, idx int, err error) {
	var zero T

	n, idx, err := parseNum(typ, data)
	if err != nil {
		return zero, 0, err
	}

	if n == -1 {
		return zero, idx, nil
	}

	length := len(data) - idx - len(separator)
	if length < n {
		return zero, idx, PayloadError("insufficient data")
	}

	if data[idx+n] != separator[0] || data[idx+n+1] != separator[1] {
		return zero, idx, PayloadError("missing separator")
	}

	return T(data[idx : idx+n]), idx + n + len(separator), nil
}

func String(data []byte) (string, error) {
	str, _, err := parseStr('+', data)
	return str, err
}

func Number(data []byte) (int, error) {
	num, _, err := parseNum(':', data)
	return num, err
}

func Blob[T Bytes](data []byte) (T, error) {
	blob, _, err := parseBlob[T]('$', data)
	return blob, err
}

func BlobArray[T Bytes](data []byte) ([]T, error) {
	n, idx, err := parseNum('*', data)
	if err != nil {
		return nil, err
	}

	ary := make([]T, n)
	for i := 0; i < n; i++ {
		data = data[idx:]
		ary[i], idx, err = parseBlob[T]('$', data)
		if err != nil {
			return nil, err
		}
	}

	return ary, nil
}
