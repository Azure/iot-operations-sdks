// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.8.0.0; DO NOT EDIT.
package schemaregistry

import (
	"encoding/json"
	"errors"
)

type Format int32

const (
	Delta1 Format = iota
	JsonSchemaDraft07  = iota
)

func (v Format) String() string {
	switch v {
	case Delta1:
		return "Delta1"
	case JsonSchemaDraft07:
		return "JsonSchemaDraft07"
	default:
		return ""
	}
}

func (v Format) MarshalJSON() ([]byte, error) {
	var s string
	switch v {
	case Delta1:
		s = "Delta/1.0"
	case JsonSchemaDraft07:
		s = "JsonSchema/draft-07"
	default:
		return []byte{}, errors.New("unable to marshal unrecognized enum value to JSON")
	}

	return json.Marshal(s)
}

func (v *Format) UnmarshalJSON(b []byte) error {
	var s string
	if err := json.Unmarshal(b, &s); err != nil {
		return err
	}

	switch s {
	case "Delta/1.0":
		*v = Delta1
	case "JsonSchema/draft-07":
		*v = JsonSchemaDraft07
	default:
		return errors.New("unable to unmarshal unrecognized enum value from JSON")
	}

	return nil
}
