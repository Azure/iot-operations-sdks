// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"errors"
	"slices"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

func (serializer *TestCaseSerializer) Serialize(
	payload string,
) (*protocol.Data, error) {
	return &protocol.Data{
		Payload:       []byte(payload),
		ContentType:   *serializer.OutContentType,
		PayloadFormat: map[bool]byte{true: 1, false: 0}[serializer.IndicateCharacterData],
	}, nil
}

func (serializer *TestCaseSerializer) Deserialize(
	data *protocol.Data,
) (string, error) {
	if !slices.Contains(serializer.AcceptContentTypes, data.ContentType) {
		return "", protocol.ErrUnsupportedContentType
	}

	if data.PayloadFormat == 1 && !serializer.AllowCharacterData {
		return "", errors.New("unsupported payload format")
	}

	if serializer.FailDeserialization {
		return "", errors.New("deserialization failure")
	}

	return string(data.Payload), nil
}
