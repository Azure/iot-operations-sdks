package resp

import (
	"bytes"
	"fmt"
	"strconv"

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
		return "", 0, errors.Response(arg)
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

func parseBlob(typ byte, data []byte) (blob []byte, idx int, err error) {
	n, idx, err := parseNum(typ, data)
	if err != nil {
		return nil, 0, err
	}

	if n == -1 {
		return nil, idx, nil
	}

	length := len(data) - idx - len(separator)
	if length < n {
		return nil, idx, PayloadError("insufficient data")
	}

	if data[idx+n] != separator[0] || data[idx+n+1] != separator[1] {
		return nil, idx, PayloadError("missing separator")
	}

	return data[idx : idx+n], idx + n + len(separator), nil
}

func ParseString(data []byte) (string, error) {
	str, _, err := parseStr('+', data)
	return str, err
}

func ParseNumber(data []byte) (int, error) {
	num, _, err := parseNum(':', data)
	return num, err
}

func ParseBlob(data []byte) ([]byte, error) {
	blob, _, err := parseBlob('$', data)
	return blob, err
}

func ParseBlobArray(data []byte) ([][]byte, error) {
	n, idx, err := parseNum('*', data)
	if err != nil {
		return nil, err
	}

	ary := make([][]byte, n)
	for i := 0; i < n; i++ {
		data = data[idx:]
		ary[i], idx, err = parseBlob('$', data)
		if err != nil {
			return nil, err
		}
	}

	return ary, nil
}
