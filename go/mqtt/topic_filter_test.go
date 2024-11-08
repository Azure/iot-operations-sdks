// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt_test

import (
	"testing"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/stretchr/testify/require"
)

func TestTopicFilterMatch(t *testing.T) {
	tests := []struct {
		filter   string
		topic    string
		expected bool
	}{
		{"$share/groups/color/+/white", "color/pink/white", true},
		{"$share/groups", "color/pink/white", false},
		{"color/+/white", "color/pink/white", true},
		{"color/+/white", "color/blue/white", true},
		{"color/+/white", "color/pink/white/shade", false},
		{"color/#", "color", true},
		{"color/#", "color/pink", true},
		{"color/#", "color/pink/white", true},
		{"color/pink", "color/pink", true},
		{"color/pink", "color/blue", false},
		{"color/+/white/#", "color/pink/white", true},
		{"color/+/white/#", "color/blue/white/shade", true},
		{"color/+/white/#", "color/pink/white/shade/details", true},
		{"color/+/white/#", "color/blue/white", true},
		{"color/#/white", "color/pink/white", false}, // Invalid filter
	}

	for _, test := range tests {
		isMatched := mqtt.IsTopicFilterMatch(test.filter, test.topic)
		require.Equal(
			t,
			test.expected,
			isMatched,
			"Topic filter: %s, Topic name: %s",
			test.filter,
			test.topic,
		)
	}
}
