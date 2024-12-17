// Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT.
package dtmi_ms_adr_SchemaRegistry__1

import (
	"encoding/json"
	"errors"
)

type Enum_Ms_Adr_SchemaRegistry_Format__1 int32

const (
	Delta1 Enum_Ms_Adr_SchemaRegistry_Format__1 = iota
	JsonSchemaDraft07  = iota
)

func (v Enum_Ms_Adr_SchemaRegistry_Format__1) String() string {
	switch v {
	case Delta1:
		return "Delta1"
	case JsonSchemaDraft07:
		return "JsonSchemaDraft07"
	default:
		return ""
	}
}

func (v Enum_Ms_Adr_SchemaRegistry_Format__1) MarshalJSON() ([]byte, error) {
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

func (v *Enum_Ms_Adr_SchemaRegistry_Format__1) UnmarshalJSON(b []byte) error {
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
