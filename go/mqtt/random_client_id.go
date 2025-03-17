// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"math/rand"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
)

// ClientIDs must be between 1 and 23 UTF-8 encoded bytes in length and only
// contain alphanumeric characters:
// https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901059
const maxClientIDLength = 23

var validClientIDCharacters = []byte(
	"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
)

// RandomClientID generates a random valid MQTT client ID. This should never be
// used in production (as it fully invalidates all session guarantees) but can
// be useful in testing to prevent parallel tests from conflicting.
func RandomClientID() string {
	seed := wallclock.Instance.Now().UnixNano()
	// #nosec G404
	r := rand.New(rand.NewSource(seed))

	id := make([]byte, maxClientIDLength)
	for i := range id {
		id[i] = validClientIDCharacters[r.Intn(len(validClientIDCharacters))]
	}
	return string(id)
}
