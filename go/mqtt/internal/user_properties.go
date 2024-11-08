// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import "github.com/eclipse/paho.golang/paho"

// UserPropertiesToMap converts userProperties to a map[string]string.
func UserPropertiesToMap(ups paho.UserProperties) map[string]string {
	m := make(map[string]string, len(ups))
	for _, prop := range ups {
		m[prop.Key] = prop.Value
	}
	return m
}

// MapToUserProperties converts a map[string]string to userProperties.
func MapToUserProperties(m map[string]string) paho.UserProperties {
	ups := make(paho.UserProperties, 0, len(m))
	for key, value := range m {
		ups = append(ups, paho.UserProperty{
			Key:   SanitizeString(key),
			Value: SanitizeString(value),
		})
	}
	return ups
}
