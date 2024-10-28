// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import (
	"maps"
	"regexp"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

type (
	// Structure to apply tokens to a named topic pattern.
	TopicPattern struct {
		name    string
		pattern string
		tokens  map[string]string
	}

	// Structure to provide a topic filter that can parse out its named tokens.
	TopicFilter struct {
		filter string
		regex  *regexp.Regexp
		names  []string
		tokens map[string]string
	}
)

const (
	topicLabel = `[^ "+#{}/]+`
	topicToken = `\{` + topicLabel + `\}`
	topicLevel = `(` + topicLabel + `|` + topicToken + `)`
	topicMatch = `(` + topicLabel + `)`
)

var (
	matchLabel = regexp.MustCompile(
		`^` + topicLabel + `$`,
	)
	matchToken = regexp.MustCompile(
		topicToken, // Lacks anchors because it is used for replacements.
	)
	matchTopic = regexp.MustCompile(
		`^` + topicLabel + `(/` + topicLabel + `)*$`,
	)
	matchPattern = regexp.MustCompile(
		`^` + topicLevel + `(/` + topicLevel + `)*$`,
	)
)

// Perform initial validation of a topic pattern component.
func ValidateTopicPatternComponent(
	name, msgOnErr, pattern string,
) error {
	if !matchPattern.MatchString(pattern) {
		return &errors.Error{
			Message:       msgOnErr,
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  name,
			PropertyValue: pattern,
		}
	}

	return nil
}

// Create a new topic pattern and perform initial validations.
func NewTopicPattern(
	name, pattern string,
	tokens map[string]string,
	namespace string,
) (*TopicPattern, error) {
	if namespace != "" {
		if !ValidTopic(namespace) {
			return nil, &errors.Error{
				Message:       "invalid topic namespace",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "TopicNamespace",
				PropertyValue: namespace,
			}
		}
		pattern = namespace + `/` + pattern
	}

	if !matchPattern.MatchString(pattern) {
		return nil, &errors.Error{
			Message:       "invalid topic pattern",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  name,
			PropertyValue: pattern,
		}
	}

	if err := validateTokens(errors.ConfigurationInvalid, tokens); err != nil {
		return nil, err
	}
	for token, value := range tokens {
		pattern = strings.ReplaceAll(pattern, `{`+token+`}`, value)
	}

	return &TopicPattern{name, pattern, tokens}, nil
}

// Fully resolve a topic pattern for publishing.
func (tp *TopicPattern) Topic(tokens map[string]string) (string, error) {
	topic := tp.pattern

	if err := validateTokens(errors.ArgumentInvalid, tokens); err != nil {
		return "", err
	}
	for token, value := range tokens {
		topic = strings.ReplaceAll(topic, `{`+token+`}`, value)
	}

	if !ValidTopic(topic) {
		missingToken := matchToken.FindString(topic)
		if missingToken != "" {
			return "", &errors.Error{
				Message:      "invalid topic",
				Kind:         errors.ArgumentInvalid,
				PropertyName: missingToken[1 : len(missingToken)-1],
			}
		}

		return "", &errors.Error{
			Message:       "invalid topic",
			Kind:          errors.ArgumentInvalid,
			PropertyName:  tp.name,
			PropertyValue: topic,
		}
	}
	return topic, nil
}

// Generate a filter for subscribing. Unresolved tokens are treated as "+"
// wildcards for this purpose.
func (tp *TopicPattern) Filter() (*TopicFilter, error) {
	// Get the remaining token names.
	names := matchToken.FindAllString(tp.pattern, -1)
	for i, token := range names {
		names[i] = token[1 : len(token)-1]
	}

	// Build a regexp matching all remaining tokens.
	escaped := regexp.QuoteMeta(tp.pattern)
	for _, token := range names {
		escaped = strings.ReplaceAll(escaped, `\{`+token+`\}`, topicMatch)
	}
	regex, err := regexp.Compile(escaped)
	if err != nil {
		return nil, err
	}

	// Replace remaining tokens with "+".
	filter := matchToken.ReplaceAllString(tp.pattern, `+`)

	return &TopicFilter{filter, regex, names, tp.tokens}, nil
}

// Filter provides the MQTT topic filter string.
func (tf *TopicFilter) Filter() string {
	return tf.filter
}

// Tokens indicates whether the topic matched and resolves its topic tokens.
func (tf *TopicFilter) Tokens(topic string) (map[string]string, bool) {
	match := tf.regex.FindStringSubmatch(topic)
	if match == nil {
		return nil, false
	}

	tokens := make(map[string]string, len(tf.names)+len(tf.tokens))
	for i, val := range match[1:] {
		tokens[tf.names[i]] = val
	}
	maps.Copy(tokens, tf.tokens)
	return tokens, true
}

// Return whether the provided string is a fully-resolved topic.
func ValidTopic(topic string) bool {
	return matchTopic.MatchString(topic)
}

// Return whether the provided string is a valid share name.
func ValidateShareName(shareName string) error {
	if shareName != "" && !matchLabel.MatchString(shareName) {
		return &errors.Error{
			Message:       "invalid share name",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  "ShareName",
			PropertyValue: shareName,
		}
	}
	return nil
}

// Return whether all the topic tokens are valid (to provide more specific
// errors compared to just testing the resulting topic). Takes the error kind as
// an argument since it may vary between ConfigurationInvalid (tokens provided
// in the constructor) and ArgumentInvalid (tokens provided at call time).
func validateTokens(kind errors.Kind, tokens map[string]string) error {
	for k, v := range tokens {
		// We don't check for the presence of token names in the pattern because
		// it's valid to provide token values that aren't in the pattern. We do,
		// however, check to make sure they're valid token names so that we can
		// warn the user in cases that will never actually be valid.
		if !matchLabel.MatchString(k) || !matchLabel.MatchString(v) {
			return &errors.Error{
				Message:       "invalid topic token",
				Kind:          kind,
				PropertyName:  k,
				PropertyValue: v,
			}
		}
	}
	return nil
}
