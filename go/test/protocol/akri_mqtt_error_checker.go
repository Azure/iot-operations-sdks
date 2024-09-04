package protocol

import (
	"fmt"
	"strings"
	"testing"

	"github.com/sosodev/duration"
	"github.com/stretchr/testify/require"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

func CheckError(t *testing.T, testCaseCatch TestCaseCatch, err error) {
	akriErr, ok := err.(*errors.Error)
	require.True(t, ok)

	require.Equal(t, testCaseCatch.GetErrorKind(), akriErr.Kind)
	require.Equal(t, testCaseCatch.InApplication, akriErr.InApplication)
	require.Equal(t, testCaseCatch.IsShallow, akriErr.IsShallow)
	require.Equal(t, testCaseCatch.IsRemote, akriErr.IsRemote)

	if testCaseCatch.StatusCode == nil {
		require.Nil(t, akriErr.HTTPStatusCode)
	} else if statusCode, ok := testCaseCatch.StatusCode.(int); ok {
		require.Equal(t, statusCode, akriErr.HTTPStatusCode)
	}

	if testCaseCatch.Message != nil {
		require.Equal(t, *testCaseCatch.Message, akriErr.Message)
	}

	if headerName, ok := testCaseCatch.Supplemental[HeaderNameKey]; ok {
		if headerName == nil {
			require.Empty(t, akriErr.HeaderName)
		} else {
			require.Equal(t, *headerName, akriErr.HeaderName)
		}
	}

	if headerValue, ok := testCaseCatch.Supplemental[HeaderValueKey]; ok {
		if headerValue == nil {
			require.Empty(t, akriErr.HeaderValue)
		} else {
			require.Equal(t, *headerValue, akriErr.HeaderValue)
		}
	}

	if timeoutName, ok := testCaseCatch.Supplemental[TimeoutNameKey]; ok {
		if timeoutName == nil {
			require.Empty(t, akriErr.TimeoutName)
		} else {
			require.Equal(t, *timeoutName, strings.ToLower(akriErr.TimeoutName))
		}
	}

	if timeoutValue, ok := testCaseCatch.Supplemental[TimeoutValueKey]; ok {
		if timeoutValue == nil {
			require.Empty(t, akriErr.TimeoutValue)
		} else {
			require.Equal(t, *timeoutValue, duration.Format(akriErr.TimeoutValue))
		}
	}

	if propertyName, ok := testCaseCatch.Supplemental[PropertyNameKey]; ok {
		if propertyName == nil {
			require.Empty(t, akriErr.PropertyName)
		} else {
			comps := strings.Split(akriErr.PropertyName, ".")
			require.Equal(t, *propertyName, strings.ToLower(comps[len(comps)-1]))
		}
	}

	if propertyValue, ok := testCaseCatch.Supplemental[PropertyValueKey]; ok {
		if propertyValue == nil {
			require.Empty(t, akriErr.PropertyValue)
		} else {
			val := fmt.Sprintf("%v", akriErr.PropertyValue)
			require.Equal(t, *propertyValue, val)
		}
	}
}
