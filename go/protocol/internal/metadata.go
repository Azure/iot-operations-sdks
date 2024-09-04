package internal

import (
	"strings"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
)

func MetadataToProp(data map[string]string) (map[string]string, error) {
	if data == nil {
		data = map[string]string{}
	}
	for k := range data {
		if strings.HasPrefix(k, constants.Protocol) {
			return nil, &errors.Error{
				Message:       "user metadata property starts with __",
				Kind:          errors.ArgumentInvalid,
				PropertyName:  "Metadata",
				PropertyValue: k,
			}
		}
	}
	return data, nil
}

func PropToMetadata(prop map[string]string) map[string]string {
	data := make(map[string]string, len(prop))
	for key, val := range prop {
		if !strings.HasPrefix(key, constants.Protocol) {
			data[key] = val
		}
	}
	return data
}
