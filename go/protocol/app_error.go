// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package protocol

import "encoding/json"

const (
	ApplicationErrorCode = "ApplicationErrorCode"
	ApplicationErrorData = "ApplicationErrorData"
)

// WithApplicationError sends an application error in the metadata using a
// standardized format.
func WithApplicationError[T any](code string, data T) interface {
	InvokeOption
	RespondOption
	SendOption
} {
	body, err := json.Marshal(data)
	if err != nil {
		// If we can't serialize the data, at least send the code.
		return WithMetadata{ApplicationErrorCode: code}
	}
	return WithMetadata{
		ApplicationErrorCode: code,
		ApplicationErrorData: string(body),
	}
}

// GetApplicationError extracts an application error (if any) from the metadata
// using a standardized format.
func GetApplicationError[T any](
	meta map[string]string,
) (code string, data T, err error) {
	if c, ok := meta[ApplicationErrorCode]; ok {
		code = c
	}
	if d, ok := meta[ApplicationErrorData]; ok {
		err = json.Unmarshal([]byte(d), &data)
	}
	return code, data, err
}
