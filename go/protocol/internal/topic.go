package internal

import (
	"fmt"
	"regexp"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
)

// Structure to apply tokens to a named topic pattern.
type TopicPattern struct {
	Name    string
	Pattern string
}

var (
	topicLabel = `[^ +#{}/]+`
	topicToken = fmt.Sprintf(`{%s}`, topicLabel)
	topicLevel = fmt.Sprintf(`(%s|%s)`, topicLabel, topicToken)

	matchLabel = regexp.MustCompile(fmt.Sprintf(`^%s$`, topicLabel))
	matchToken = regexp.MustCompile(topicToken) // Used for replace.
	matchTopic = regexp.MustCompile(
		fmt.Sprintf(`^%s(/%s)*$`, topicLabel, topicLabel),
	)
	matchPattern = regexp.MustCompile(
		fmt.Sprintf(`^%s(/%s)*$`, topicLevel, topicLevel),
	)
)

// Create a new topic pattern and perform initial validations.
func NewTopicPattern(
	name, pattern string,
	tokens map[string]string,
	namespace string,
) (TopicPattern, error) {
	if namespace != "" {
		if !ValidTopic(namespace) {
			return TopicPattern{}, &errors.Error{
				Message:       "invalid topic namespace",
				Kind:          errors.ConfigurationInvalid,
				PropertyName:  "TopicNamespace",
				PropertyValue: namespace,
			}
		}
		pattern = namespace + "/" + pattern
	}
	if err := validateTokens(errors.ConfigurationInvalid, tokens); err != nil {
		return TopicPattern{}, err
	}
	tp := TopicPattern{name, pattern}.Apply(tokens)
	if err := tp.validate(); err != nil {
		return TopicPattern{}, err
	}
	return tp, nil
}

// Verify the format of the topic pattern.
func (tp TopicPattern) validate() error {
	if !matchPattern.MatchString(tp.Pattern) {
		return &errors.Error{
			Message:       "invalid topic pattern",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  tp.Name,
			PropertyValue: tp.Pattern,
		}
	}
	return nil
}

// Apply the given token values to the MQTT topic and return the result (which
// may still have remaining unresolved tokens).
func (tp TopicPattern) Apply(tokens map[string]string) TopicPattern {
	next := tp
	for token, value := range tokens {
		next.Pattern = strings.ReplaceAll(next.Pattern, "{"+token+"}", value)
	}
	return next
}

// Fully resolve a topic pattern for publishing.
func (tp TopicPattern) Topic(tokens map[string]string) (string, error) {
	if err := validateTokens(errors.ArgumentInvalid, tokens); err != nil {
		return "", err
	}
	topic := tp.Apply(tokens).Pattern
	if !ValidTopic(topic) {
		return "", &errors.Error{
			Message:       "invalid topic",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  tp.Name,
			PropertyValue: tp.Pattern,
		}
	}
	return topic, nil
}

// Generate a regexp for subscribing. Unresolved tokens are treated as "+"
// wildcards for this purpose.
func (tp TopicPattern) Filter() (string, error) {
	if err := tp.validate(); err != nil {
		return "", err
	}
	return matchToken.ReplaceAllString(tp.Pattern, "+"), nil
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
