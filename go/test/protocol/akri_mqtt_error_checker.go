// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"fmt"
	"strings"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/sosodev/duration"
	"github.com/stretchr/testify/require"
)

func CheckError(t *testing.T, testCaseCatch TestCaseCatch, err error) {
	if remoteErr, ok := err.(*errors.Remote); ok {
		require.Equal(t, testCaseCatch.GetErrorKind(), remoteErr.Kind)

		if testCaseCatch.InApplication != nil {
			require.Equal(
				t,
				*testCaseCatch.InApplication,
				remoteErr.InApplication,
			)
		}

		if testCaseCatch.StatusCode == nil {
			require.Zero(t, remoteErr.HTTPStatusCode)
		} else if statusCode, ok := testCaseCatch.StatusCode.(int); ok {
			require.Equal(t, statusCode, remoteErr.HTTPStatusCode)
		}

		checkCommonFields(t, testCaseCatch, &remoteErr.Base)
		return
	}

	if clientErr, ok := err.(*errors.Client); ok {
		require.Equal(t, testCaseCatch.GetErrorKind(), clientErr.Kind)

		if testCaseCatch.IsShallow != nil {
			require.Equal(t, *testCaseCatch.IsShallow, clientErr.IsShallow)
		}

		checkCommonFields(t, testCaseCatch, &clientErr.Base)
		return
	}

	require.Fail(t, "error must be either Remote or Client")
}

func checkCommonFields(
	t *testing.T,
	testCaseCatch TestCaseCatch,
	baseErr *errors.Base,
) {
	if testCaseCatch.Message != nil {
		require.Equal(t, *testCaseCatch.Message, baseErr.Message)
	}

	if headerName, ok := testCaseCatch.Supplemental[HeaderNameKey]; ok {
		if headerName == nil {
			require.Empty(t, baseErr.HeaderName)
		} else {
			require.Equal(t, *headerName, baseErr.HeaderName)
		}
	}

	if headerValue, ok := testCaseCatch.Supplemental[HeaderValueKey]; ok {
		if headerValue == nil {
			require.Empty(t, baseErr.HeaderValue)
		} else {
			require.Equal(t, *headerValue, baseErr.HeaderValue)
		}
	}

	if timeoutName, ok := testCaseCatch.Supplemental[TimeoutNameKey]; ok {
		if timeoutName == nil {
			require.Empty(t, baseErr.TimeoutName)
		} else {
			require.Equal(t, *timeoutName, strings.ToLower(baseErr.TimeoutName))
		}
	}

	if timeoutValue, ok := testCaseCatch.Supplemental[TimeoutValueKey]; ok {
		if timeoutValue == nil {
			require.Empty(t, baseErr.TimeoutValue)
		} else {
			require.Equal(t, *timeoutValue, duration.Format(baseErr.TimeoutValue))
		}
	}

	if propertyName, ok := testCaseCatch.Supplemental[PropertyNameKey]; ok {
		if propertyName == nil {
			require.Empty(t, baseErr.PropertyName)
		} else {
			comps := strings.Split(baseErr.PropertyName, ".")
			require.Equal(t, *propertyName, strings.ToLower(comps[len(comps)-1]))
		}
	}

	if propertyValue, ok := testCaseCatch.Supplemental[PropertyValueKey]; ok {
		if propertyValue == nil {
			require.Empty(t, baseErr.PropertyValue)
		} else {
			val := fmt.Sprintf("%v", baseErr.PropertyValue)
			require.Equal(t, *propertyValue, val)
		}
	}
}
