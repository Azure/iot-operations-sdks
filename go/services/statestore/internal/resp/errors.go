package resp

import "github.com/Azure/iot-operations-sdks/go/services/statestore/internal"

func ErrMissingSeparator(op string) error {
	return &internal.Error{
		Operation: op,
		Message:   "missing separator",
	}
}

func ErrWrongType(op string, typ byte) error {
	return &internal.Error{
		Operation: op,
		Message:   "wrong type",
		Value:     string([]byte{typ}),
	}
}

func ErrInvalidNumber(op, val string) error {
	return &internal.Error{
		Operation: op,
		Message:   "invalid number",
		Value:     val,
	}
}

func ErrInsufficientData(op string) error {
	return &internal.Error{
		Operation: op,
		Message:   "insufficient data",
	}
}
