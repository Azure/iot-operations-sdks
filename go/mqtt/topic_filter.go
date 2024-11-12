// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import "strings"

const sharedPrefix = "$share/"

// IsTopicFilterMatch checks if a topic name matches a topic filter.
func IsTopicFilterMatch(topicFilter, topicName string) bool {
	// Handle shared subscriptions.
	if tf, ok := strings.CutPrefix(topicFilter, sharedPrefix); ok {
		// Find the index of the second slash.
		idx := strings.Index(tf, "/")
		if idx == -1 {
			// Invalid shared subscription format.
			return false
		}
		topicFilter = tf[idx+1:]
	}

	filters := strings.Split(topicFilter, "/")
	names := strings.Split(topicName, "/")

	for i, filter := range filters {
		if filter == "#" {
			// Multi-level wildcard must be at the end.
			return i == len(filters)-1
		}
		if filter == "+" {
			// Single-level wildcard matches any single level.
			continue
		}
		if i >= len(names) || filter != names[i] {
			return false
		}
	}

	// Exact match is required if there are no wildcards left.
	return len(filters) == len(names)
}
