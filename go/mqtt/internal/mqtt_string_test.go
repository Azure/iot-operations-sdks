// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal_test

import (
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/stretchr/testify/require"
)

func TestSanitizeString(t *testing.T) {
	tests := []struct {
		name     string
		input    string
		expected string
	}{
		{
			name:     "Empty string",
			input:    "",
			expected: "",
		},
		{
			name:     "Valid string",
			input:    "Great power, great irresponsibility",
			expected: "Great power, great irresponsibility",
		},
		{
			name:     "String with newline",
			input:    "Great power\n great irresponsibility\n",
			expected: "Great power great irresponsibility",
		},
		{
			name:     "String with control characters",
			input:    "Great power\x01, great\x0F irresponsibility",
			expected: "Great power, great irresponsibility",
		},
		{
			name:     "String with non-characters",
			input:    "Great power\uFDD0, great\uFDEF irresponsibility",
			expected: "Great power, great irresponsibility",
		},
		{
			name:     "String with other non-characters",
			input:    "Great power\uFFFE\uFFFF, great irresponsibility",
			expected: "Great power, great irresponsibility",
		},
		{
			name:     "Invalid string",
			input:    "\x01\x7F\uFDD0\uFFFE\uFFFF",
			expected: "",
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			result := internal.SanitizeString(test.input)
			if result != test.expected {
				require.Equal(t, test.expected, result)
			}
		})
	}
}
