// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"fmt"
	"strconv"
	"strings"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/sosodev/duration"
	"github.com/stretchr/testify/require"
)

func CheckError(t *testing.T, testCaseCatch TestCaseCatch, err error) {
	switch e := err.(type) {
	case *errors.Remote:
		if testCaseCatch.IsRemote != nil {
			require.True(t, *testCaseCatch.IsRemote)
		}

		if testCaseCatch.Message != nil {
			require.Equal(t, *testCaseCatch.Message, e.Message)
		}

		require.Equal(t, testCaseCatch.ErrorKind, e.Kind.String())

		checkKindFields(t, testCaseCatch, e.Kind)

	case *errors.Client:
		if testCaseCatch.IsRemote != nil {
			require.False(t, *testCaseCatch.IsRemote)
		}

		if testCaseCatch.Message != nil {
			require.Equal(t, *testCaseCatch.Message, e.Message)
		}

		require.Equal(t, testCaseCatch.ErrorKind, e.Kind.String())

		if testCaseCatch.IsShallow != nil {
			require.Equal(t, *testCaseCatch.IsShallow, e.Shallow)
		}

		checkKindFields(t, testCaseCatch, e.Kind)

	default:
		require.Fail(t, "error must be either Remote or Client")
	}
}

func checkKindFields(
	t *testing.T,
	testCaseCatch TestCaseCatch,
	kind errors.Kind,
) {
	if headerName, ok := testCaseCatch.Supplemental[HeaderNameKey]; ok {
		switch k := kind.(type) {
		case errors.HeaderMissing:
			require.Equal(t, headerName, k.HeaderName)
		case errors.HeaderInvalid:
			require.Equal(t, headerName, k.HeaderName)
		default:
			failKind(t, HeaderNameKey, kind)
		}
	}

	if headerValue, ok := testCaseCatch.Supplemental[HeaderValueKey]; ok {
		if k, ok := kind.(errors.HeaderInvalid); ok {
			require.Equal(t, headerValue, k.HeaderValue)
		} else {
			failKind(t, HeaderValueKey, kind)
		}
	}

	if timeoutName, ok := testCaseCatch.Supplemental[TimeoutNameKey]; ok {
		if k, ok := kind.(errors.Timeout); ok {
			require.Equal(t, timeoutName, strings.ToLower(k.TimeoutName))
		} else {
			failKind(t, TimeoutNameKey, kind)
		}
	}

	if timeoutValue, ok := testCaseCatch.Supplemental[TimeoutValueKey]; ok {
		if k, ok := kind.(errors.Timeout); ok {
			require.Equal(t, timeoutValue, duration.Format(k.TimeoutValue))
		} else {
			failKind(t, TimeoutValueKey, kind)
		}
	}

	if propertyName, ok := testCaseCatch.Supplemental[PropertyNameKey]; ok {
		switch k := kind.(type) {
		case errors.ConfigurationInvalid:
			comps := strings.Split(k.PropertyName, ".")
			require.Equal(t, propertyName, strings.ToLower(comps[len(comps)-1]))
		case errors.StateInvalid:
			comps := strings.Split(k.PropertyName, ".")
			require.Equal(t, propertyName, strings.ToLower(comps[len(comps)-1]))
		case errors.InternalLogicError:
			//nolint:staticcheck // Capture for wire protocol compat.
			comps := strings.Split(k.PropertyName, ".")
			require.Equal(t, propertyName, strings.ToLower(comps[len(comps)-1]))
		case errors.UnknownError:
			//nolint:staticcheck // Capture 422 data for schemaregistry.
			comps := strings.Split(k.PropertyName, ".")
			require.Equal(t, propertyName, strings.ToLower(comps[len(comps)-1]))
		default:
			failKind(t, PropertyNameKey, kind)
		}
	}

	if propertyValue, ok := testCaseCatch.Supplemental[PropertyValueKey]; ok {
		switch k := kind.(type) {
		case errors.ConfigurationInvalid:
			val := fmt.Sprintf("%v", k.PropertyValue)
			require.Equal(t, propertyValue, val)
		case errors.UnknownError:
			//nolint:staticcheck // Capture 422 data for schemaregistry.
			val := fmt.Sprintf("%v", k.PropertyValue)
			require.Equal(t, propertyValue, val)
		default:
			failKind(t, PropertyValueKey, kind)
		}
	}

	if protocolVersion, ok := testCaseCatch.Supplemental[ProtocolVersionKey]; ok {
		if k, ok := kind.(errors.UnsupportedVersion); ok {
			require.Equal(t, protocolVersion, k.ProtocolVersion)
		} else {
			failKind(t, ProtocolVersionKey, kind)
		}
	}

	if supportedProtocols, ok := testCaseCatch.Supplemental[SupportedProtocolsKey]; ok {
		if k, ok := kind.(errors.UnsupportedVersion); ok {
			res := make([]string, len(k.SupportedMajorProtocolVersions))
			for i, n := range k.SupportedMajorProtocolVersions {
				res[i] = strconv.Itoa(n)
			}
			require.Equal(t, supportedProtocols, strings.Join(res, " "))
		} else {
			failKind(t, SupportedProtocolsKey, kind)
		}
	}
}

func failKind(t *testing.T, field string, kind errors.Kind) {
	require.Fail(t,
		fmt.Sprintf(`%q does not have field %q`, kind.String(), field))
}
