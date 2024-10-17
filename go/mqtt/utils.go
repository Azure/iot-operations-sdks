// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/tls"
	"crypto/x509"
	"encoding/pem"
	"errors"
	"math/rand"
	"os"
	"regexp"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/eclipse/paho.golang/paho"
	"golang.org/x/crypto/pbkdf2"
	"golang.org/x/crypto/sha3"
)

// randomClientID generates a random ClientID of the specified length
// containing only lowercase/uppercase letters and numbers.
//
// From MQTT specification: The Server MUST allow ClientID's
// which are between 1 and 23 UTF-8 encoded bytes in length,
// and that contain only the characters
// "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
// [MQTT-3.1.3-5].
func randomClientID() string {
	// "sessionclient" + a 10-byte ID results in a total length of 23 bytes.
	id := make([]byte, 10)
	for i := range id {
		id[i] = randomChar()
	}
	return "sessionclient" + string(id)
}

// randomChar generates a random character from the allowed set [0-9a-zA-Z].
func randomChar() byte {
	seed := wallclock.Instance.Now().UnixNano()
	// #nosec G404
	r := rand.New(rand.NewSource(seed))

	num := r.Intn(62)
	switch {
	case num < 10:
		return '0' + byte(num)
	case num < 36:
		return 'a' + byte(num-10)
	default:
		return 'A' + byte(num-36)
	}
}

// IsTopicFilterMatch checks if a topic name matches a topic filter, including
// handling shared subscriptions.
func IsTopicFilterMatch(topicFilter, topicName string) bool {
	const sharedPrefix = "$share/"

	// Handle shared subscriptions
	if strings.HasPrefix(topicFilter, sharedPrefix) {
		// Find the index of the second slash
		secondSlashIdx := strings.Index(topicFilter[len(sharedPrefix):], "/")
		if secondSlashIdx == -1 {
			// Invalid shared subscription format
			return false
		}
		topicFilter = topicFilter[len(sharedPrefix)+secondSlashIdx+1:]
	}

	// Return false if the multi-level wildcard is not at the end
	if strings.Contains(topicFilter, "#") &&
		!strings.HasSuffix(topicFilter, "/#") {
		return false
	}

	filters := strings.Split(topicFilter, "/")
	names := strings.Split(topicName, "/")

	for i, filter := range filters {
		if filter == "#" {
			// Multi-level wildcard must be at the end
			return i == len(filters)-1
		}
		if filter == "+" {
			// Single-level wildcard matches any single level
			continue
		}
		if i >= len(names) || filter != names[i] {
			return false
		}
	}

	// Exact match is required if there are no wildcards left
	return len(filters) == len(names)
}

// readFileAsBytes reads the entire file content into a byte slice.
func readFileAsBytes(filePath string) ([]byte, error) {
	data, err := os.ReadFile(filePath)
	if err != nil {
		return nil, err
	}
	return data, nil
}

// loadCACertPool loads a CA certificate pool from the specified file.
func loadCACertPool(caFile string) (*x509.CertPool, error) {
	caCert, err := os.ReadFile(caFile)
	if err != nil {
		return nil, err
	}
	caCertPool := x509.NewCertPool()
	if !caCertPool.AppendCertsFromPEM(caCert) {
		return nil, err
	}
	return caCertPool, nil
}

// decryptPEMBlock decrypts a PEM block using PBKDF2 and AES-GCM.
func decryptPEMBlock(block *pem.Block, password []byte) ([]byte, error) {
	if block == nil {
		return nil, errors.New("PEM block is nil")
	}

	// Extract the salt (first 8 bytes)
	salt := block.Bytes[:8]

	// Derive key using PBKDF2
	key := pbkdf2.Key(password, salt, 10000, 32, sha3.New256)

	// Decrypt the block using AES-GCM
	return aesGCMDecrypt(block.Bytes[8:], key)
}

// aesGCMDecrypt decrypts data using AES-GCM mode.
func aesGCMDecrypt(encrypted, key []byte) ([]byte, error) {
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, err
	}

	// AES-GCM typically uses a 12-byte(96-bit) nonce(random number).
	// This size is recommended because it provides a good balance
	// between security and performance.
	if len(encrypted) < aesGcmNonce {
		return nil, errors.New("ciphertext in PEM block is too short")
	}

	nonce, ciphertext := encrypted[:12], encrypted[12:]

	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return nil, err
	}

	return gcm.Open(nil, nonce, ciphertext, nil)
}

// loadX509KeyPairWithPassword loads key pair from the encrypted file.
func loadX509KeyPairWithPassword(
	certFile,
	keyFile,
	password string,
) (tls.Certificate, error) {
	certPEMBlock, err := os.ReadFile(certFile)
	if err != nil {
		return tls.Certificate{}, err
	}

	keyPEMBlock, err := os.ReadFile(keyFile)
	if err != nil {
		return tls.Certificate{}, err
	}

	keyDERBlock, _ := pem.Decode(keyPEMBlock)
	if keyDERBlock == nil {
		return tls.Certificate{}, errors.New(
			"failed to decode PEM block containing private key",
		)
	}

	// x509.DecryptPEMBlock is deprecated due to insecurity,
	// and x509 library doesn't want to support it:
	// https://github.com/golang/go/issues/8860
	decryptedDERBlock, err := decryptPEMBlock(keyDERBlock, []byte(password))
	if err != nil {
		return tls.Certificate{}, err
	}

	decryptedPEMBlock := pem.Block{
		Type:  keyDERBlock.Type,
		Bytes: decryptedDERBlock,
	}

	keyPEM := pem.EncodeToMemory(&decryptedPEMBlock)
	cert, err := tls.X509KeyPair(certPEMBlock, keyPEM)
	if err != nil {
		return tls.Certificate{}, err
	}

	return cert, nil
}

// userPropertiesToMap converts userProperties to a map[string]string.
func userPropertiesToMap(ups paho.UserProperties) map[string]string {
	m := make(map[string]string, len(ups))
	for _, prop := range ups {
		m[prop.Key] = prop.Value
	}
	return m
}

// mapToUserProperties converts a map[string]string to userProperties.
func mapToUserProperties(m map[string]string) paho.UserProperties {
	ups := make(paho.UserProperties, 0, len(m))
	for key, value := range m {
		ups = append(ups, paho.UserProperty{
			Key:   mqttString(key),
			Value: mqttString(value),
		})
	}
	return ups
}

// validMQTTStringRegexp returns a regexp to validate a UTF-8 encoded string.
func validMQTTStringRegexp() *regexp.Regexp {
	// Control characters U+0000 to U+001F
	c0ControlChars := `[\x00-\x1F]`

	// Control characters U+007F to U+009F
	// Note that [\x80-\x9F] cannot be filtered in Golang by regex!
	// Only works for \x7F
	c1ControlChars := `[\x7F-\x9F]`

	// In particular, the character data MUST NOT include
	// encodings of code points between U+D800 and U+DFFF [MQTT-1.5.4-1].
	// UTF-8 and UTF-32 do not use surrogate pairs and it's only for UTF-16.
	surrogates := `[\x{D800}-\x{DFFF}]`

	// Non-characters U+FDD0 to U+FDEF
	nonChars1 := `[\x{FDD0}-\x{FDEF}]`

	// Non-characters U+FFFE and U+FFFF
	nonChars2 := `[\x{FFFE}-\x{FFFF}]`

	// Combine all parts into a single regex pattern
	pattern := c0ControlChars + "|" + c1ControlChars + "|" +
		surrogates + "|" + nonChars1 + "|" + nonChars2

	return regexp.MustCompile(pattern)
}

// mqttString will replace all matches with an empty space.
func mqttString(input string) string {
	return validMQTTStringRegexp().ReplaceAllString(input, " ")
}

// An error with this wrapper is considered retryable.
type retryableErr struct{ error }

func isRetryableError(err error) bool {
	_, ok := err.(retryableErr)
	return ok
}

// Retryable reason codes for CONNACK.
var retryableConnackCodes = map[reasonCode]bool{
	connackServerUnavailable:           true,
	connackServerBusy:                  true,
	connackQuotaExceeded:               true,
	connackConnectionRateExceeded:      true,
	connackNotAuthorized:               false,
	connackMalformedPacket:             false,
	connackProtocolError:               false,
	connackBadAuthenticationMethod:     false,
	connackClientIdentifierNotValid:    false,
	connackBadUserNameOrPassword:       false,
	connackBanned:                      false,
	connackImplementationSpecificError: false,
	connackUseAnotherServer:            false,
	connackUnsupportedProtocolVersion:  false,
	connackReauthenticate:              false,
}

// Retryable reason codes for DISCONNECT.
var retryableDisconnectCodes = map[reasonCode]bool{
	disconnectServerUnavailable:                   true,
	disconnectServerBusy:                          true,
	disconnectQuotaExceeded:                       true,
	disconnectConnectionRateExceeded:              true,
	disconnectNotAuthorized:                       false,
	disconnectMalformedPacket:                     false,
	disconnectProtocolError:                       false,
	disconnectBadAuthenticationMethod:             false,
	disconnectSessionTakenOver:                    false,
	disconnectTopicFilterInvalid:                  false,
	disconnectTopicNameInvalid:                    false,
	disconnectTopicAliasInvalid:                   false,
	disconnectPacketTooLarge:                      false,
	disconnectPayloadFormatInvalid:                false,
	disconnectRetainNotSupported:                  false,
	disconnectQoSNotSupported:                     false,
	disconnectServerMoved:                         false,
	disconnectSharedSubscriptionsNotSupported:     false,
	disconnectSubscriptionIdentifiersNotSupported: false,
	disconnectWildcardSubscriptionsNotSupported:   false,
}

// isRetryableConnack checks if the reason code in Connack is retryable.
func isRetryableConnack(reasonCode reasonCode) bool {
	if retryable, exists := retryableConnackCodes[reasonCode]; exists {
		return retryable
	}
	return false
}

// isRetryableDisconnect checks if the reason code in Disconnect is retryable.
func isRetryableDisconnect(reasonCode reasonCode) bool {
	if retryable, exists := retryableDisconnectCodes[reasonCode]; exists {
		return retryable
	}
	return false
}
