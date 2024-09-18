package internal

import (
	"fmt"
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
		regexp *regexp.Regexp
		tokens []string
	}
)

var (
	topicLabel = `[^ +#{}/]+`
	topicToken = fmt.Sprintf(`{%s}`, topicLabel)
	topicLevel = fmt.Sprintf(`(%s|%s)`, topicLabel, topicToken)
	topicMatch = fmt.Sprintf(`(%s)`, topicLabel)

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
		pattern = namespace + "/" + pattern
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
	return &TopicPattern{name, pattern, tokens}, nil
}

// Fully resolve a topic pattern for publishing.
func (tp *TopicPattern) Topic(tokens map[string]string) (string, error) {
	if err := validateTokens(errors.ArgumentInvalid, tokens); err != nil {
		return "", err
	}

	topic := tp.pattern
	for token, value := range tokens {
		topic = strings.ReplaceAll(topic, "{"+token+"}", value)
	}
	for token, value := range tp.tokens {
		topic = strings.ReplaceAll(topic, "{"+token+"}", value)
	}

	if !ValidTopic(topic) {
		return "", &errors.Error{
			Message:       "invalid topic",
			Kind:          errors.ConfigurationInvalid,
			PropertyName:  tp.name,
			PropertyValue: topic,
		}
	}
	return topic, nil
}

// Generate a filter for subscribing. Unresolved tokens are treated as "+"
// wildcards for this purpose.
func (tp *TopicPattern) Filter() (*TopicFilter, error) {
	// Build a regexp matching all tokens
	rx, err := regexp.Compile(
		matchToken.ReplaceAllString(tp.pattern, topicMatch),
	)
	if err != nil {
		return nil, err
	}

	// Get the token names.
	tok := matchToken.FindAllString(tp.pattern, -1)
	tokens := make([]string, len(tok))
	for i, t := range tok {
		tokens[i] = t[1 : len(t)-1]
	}

	// Resolve specified tokens and replace the remainder with "+".
	filter := tp.pattern
	for token, value := range tp.tokens {
		filter = strings.ReplaceAll(filter, "{"+token+"}", value)
	}
	filter = matchToken.ReplaceAllString(filter, "+")

	return &TopicFilter{filter, rx, tokens}, nil
}

// Filter provides the MQTT topic filter string.
func (tf *TopicFilter) Filter() string {
	return tf.filter
}

// Tokens resolves the topic tokens from the topic.
func (tf *TopicFilter) Tokens(topic string) map[string]string {
	values := tf.regexp.FindStringSubmatch(topic)[1:]
	tokens := make(map[string]string, len(values))
	for i, val := range values {
		tokens[tf.tokens[i]] = val
	}
	return tokens
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
