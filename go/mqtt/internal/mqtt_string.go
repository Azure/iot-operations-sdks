// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package internal

import "regexp"

// Character ranges that are not valid according to MQTTv5:
// https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901010
const (
	// Control characters U+0000 to U+001F.
	c0ControlChars = `\x00-\x1F`

	// Control characters U+007F to U+009F.
	// NOTE: This only works for \x7F, due to limitations in Go's regexp.
	c1ControlChars = `\x7F-\x9F`

	// UTF-16 surrogate pairs.
	surrogates = `\x{D800}-\x{DFFF}`

	// Unicode non-characters U+FDD0 to U+FDEF.
	nonChars1 = `\x{FDD0}-\x{FDEF}`

	// Unicode non-characters U+FFFE to U+FFFF.
	nonChars2 = `\x{FFFE}-\x{FFFF}`
)

var invalidMqttCharacters = regexp.MustCompile(`[` +
	c0ControlChars +
	c1ControlChars +
	surrogates +
	nonChars1 +
	nonChars2 +
	`]`)

// SanitizeString removes all characters that are not valid in MQTT strings.
// Since they are otherwise non-printable characters, they are not replaced with
// any placeholder.
func SanitizeString(input string) string {
	return invalidMqttCharacters.ReplaceAllString(input, "")
}
