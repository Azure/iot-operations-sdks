package resp

import (
	"bytes"
	"strconv"

	"github.com/Azure/iot-operations-sdks/go/services/statestore/internal"
)

func parseStr(
	op string,
	typ byte,
	data []byte,
) (arg string, idx int, err error) {
	sep := bytes.Index(data, separator)
	if sep < 0 {
		return "", 0, &internal.Error{
			Operation: op,
			Message:   "missing separator",
		}
	}

	arg = string(data[1:sep])
	idx = sep + len(separator)

	switch data[0] {
	case '-':
		return "", 0, &internal.Error{
			Operation: op,
			Message:   arg,
		}
	case typ:
		return arg, idx, nil
	default:
		return "", 0, &internal.Error{
			Operation: op,
			Message:   "wrong type",
			Value:     string(data[:1]),
		}
	}
}

func parseNum(
	op string,
	typ byte,
	data []byte,
) (num, idx int, err error) {
	val, idx, err := parseStr(op, typ, data)
	if err != nil {
		return 0, 0, err
	}

	num, err = strconv.Atoi(val)
	if err != nil {
		return 0, 0, &internal.Error{
			Operation: op,
			Message:   "invalid number",
			Value:     val,
		}
	}

	return num, idx, nil
}

func parseBlob(
	op string,
	typ byte,
	data []byte,
) (blob []byte, idx int, err error) {
	n, idx, err := parseNum(op, typ, data)
	if err != nil {
		return nil, 0, err
	}

	if n == -1 {
		return nil, idx, nil
	}

	length := len(data) - idx - len(separator)
	if length < n {
		return nil, idx, &internal.Error{
			Operation: op,
			Message:   "insufficient data",
		}
	}

	if data[idx+n] != separator[0] || data[idx+n+1] != separator[1] {
		return nil, idx, &internal.Error{
			Operation: op,
			Message:   "missing separator",
		}
	}

	return data[idx : idx+n], idx + n + len(separator), nil
}

func ParseString(op string, data []byte) (string, error) {
	str, _, err := parseStr(op, '+', data)
	return str, err
}

func ParseNumber(op string, data []byte) (int, error) {
	num, _, err := parseNum(op, ':', data)
	return num, err
}

func ParseBlob(op string, data []byte) ([]byte, error) {
	blob, _, err := parseBlob(op, '$', data)
	return blob, err
}

func ParseBlobArray(op string, data []byte) ([][]byte, error) {
	n, idx, err := parseNum(op, '*', data)
	if err != nil {
		return nil, err
	}

	ary := make([][]byte, n)
	for i := 0; i < n; i++ {
		data = data[idx:]
		ary[i], idx, err = parseBlob(op, '$', data)
		if err != nil {
			return nil, err
		}
	}

	return ary, nil
}
