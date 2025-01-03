// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"strings"

	"github.com/Azure/iot-operations-sdks/go/protocol/internal/constants"
)

func MetadataToProp(data map[string]string) (map[string]string, error) {
	if data == nil {
		data = map[string]string{}
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
