// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

type DefaultSerializer struct {
	OutContentType        *string  `toml:"out-content-type"`
	AcceptContentTypes    []string `toml:"accept-content-types"`
	IndicateCharacterData bool     `toml:"indicate-character-data"`
	AllowCharacterData    bool     `toml:"allow-character-data"`
	FailDeserialization   bool     `toml:"fail-deserialization"`
}

func (serializer *DefaultSerializer) GetSerializer() TestCaseSerializer {
	return TestCaseSerializer{
		testCaseSerializer{
			OutContentType:        serializer.GetOutContentType(),
			AcceptContentTypes:    serializer.GetAcceptContentTypes(),
			IndicateCharacterData: serializer.GetIndicateCharacterData(),
			AllowCharacterData:    serializer.GetAllowCharacterData(),
			FailDeserialization:   serializer.GetFailDeserialization(),
		},
	}
}

func (serializer *DefaultSerializer) GetOutContentType() *string {
	if serializer.OutContentType == nil {
		return nil
	}

	outContentType := *serializer.OutContentType
	return &outContentType
}

func (serializer *DefaultSerializer) GetAcceptContentTypes() []string {
	acceptContentTypes := make([]string, len(serializer.AcceptContentTypes))
	copy(acceptContentTypes, serializer.AcceptContentTypes)
	return acceptContentTypes
}

func (serializer *DefaultSerializer) GetIndicateCharacterData() bool {
	return serializer.IndicateCharacterData
}

func (serializer *DefaultSerializer) GetAllowCharacterData() bool {
	return serializer.AllowCharacterData
}

func (serializer *DefaultSerializer) GetFailDeserialization() bool {
	return serializer.FailDeserialization
}
