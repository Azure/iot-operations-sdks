// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal_test

import (
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol/internal"
	"github.com/stretchr/testify/require"
)

func TestTopicPatternBasic(t *testing.T) {
	pattern, err := internal.NewTopicPattern(
		"basic",
		"a/{default}/topic/{pattern}",
		map[string]string{"default": "basic"},
		"",
	)
	require.NoError(t, err)

	topic, err := pattern.Topic(map[string]string{
		"default": "replaced", // Tokens provided to the constructor are static.
		"pattern": "resolved",
	})
	require.NoError(t, err)
	require.Equal(t, "a/basic/topic/resolved", topic)

	_, err = pattern.Topic(nil)
	require.Error(t, err)
	require.Equal(t, "invalid topic", err.Error())

	filter, err := pattern.Filter()
	require.NoError(t, err)
	require.Equal(t, "a/basic/topic/+", filter.Filter())
	require.Equal(t, map[string]string{
		"default": "basic",
		"pattern": "resolved",
	}, filter.Tokens(topic))
}

func TestTopicPatternMeta(t *testing.T) {
	pattern, err := internal.NewTopicPattern(
		"basic",
		"a/(topic)/pattern/{with}/[meta]/{characters}",
		map[string]string{"with": "without"},
		"",
	)
	require.NoError(t, err)

	topic, err := pattern.Topic(map[string]string{"characters": "conflicts"})
	require.NoError(t, err)
	require.Equal(t, "a/(topic)/pattern/without/[meta]/conflicts", topic)

	filter, err := pattern.Filter()
	require.NoError(t, err)
	require.Equal(t, "a/(topic)/pattern/without/[meta]/+", filter.Filter())
	require.Equal(t, map[string]string{
		"with":       "without",
		"characters": "conflicts",
	}, filter.Tokens(topic))
}
