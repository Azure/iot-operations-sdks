// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/pem"
	"errors"
	"testing"

	"github.com/stretchr/testify/require"
	"golang.org/x/crypto/pbkdf2"
	"golang.org/x/crypto/sha3"
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
		isMatched := isTopicFilterMatch(test.filter, test.topic)
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

// Mock function to create an encrypted PEM block for testing.
func createEncryptedPEMBlock(
	password []byte,
) (*pem.Block, []byte, error) {
	// Create a random salt
	salt := make([]byte, 8)
	_, err := rand.Read(salt)
	if err != nil {
		return nil, nil, err
	}

	// Derive key using PBKDF2
	key := pbkdf2.Key(password, salt, 10000, 32, sha3.New256)

	// Create a random nonce
	nonce := make([]byte, 12)
	_, err = rand.Read(nonce)
	if err != nil {
		return nil, nil, err
	}

	// Create a new AES cipher
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, nil, err
	}

	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return nil, nil, err
	}

	// Create random plaintext
	plaintext := []byte("spongebob")

	// Encrypt the plaintext
	ciphertext := gcm.Seal(nil, nonce, plaintext, nil)

	// Combine salt, nonce, and ciphertext
	encrypted := salt
	encrypted = append(encrypted, nonce...)
	encrypted = append(encrypted, ciphertext...)

	// Create a PEM block
	pemBlock := &pem.Block{
		Type:  "ENCRYPTED MESSAGE",
		Bytes: encrypted,
	}

	return pemBlock, plaintext, nil
}

func TestDecryptPEMBlock(t *testing.T) {
	password := []byte("squarepants")
	block, plaintext, err := createEncryptedPEMBlock(password)
	require.NoError(t, err)

	t.Run("ValidDecryption", func(t *testing.T) {
		decrypted, err := decryptPEMBlock(block, password)
		require.NoError(t, err)
		require.Equal(t, string(decrypted), string(plaintext))
	})

	t.Run("NilPEMBlock", func(t *testing.T) {
		_, err := decryptPEMBlock(nil, password)
		require.Error(t, errors.New("PEM block is nil"), err)
	})

	t.Run("InvalidPassword", func(t *testing.T) {
		invalidPassword := []byte("wrongpassword")
		_, err := decryptPEMBlock(block, invalidPassword)
		require.Error(
			t,
			errors.New("cipher: message authentication failed"),
			err,
		)
	})

	t.Run("TooShortCiphertext", func(t *testing.T) {
		invalidBlock := &pem.Block{
			Type:  "ENCRYPTED MESSAGE",
			Bytes: block.Bytes[:19], // Too short ciphertext
		}
		_, err := decryptPEMBlock(invalidBlock, password)
		require.Error(
			t,
			errors.New("ciphertext in PEM block is too short"),
			err,
		)
	})
}

func TestMQTTString(t *testing.T) {
	tests := []struct {
		name     string
		input    string
		expected string
	}{
		{
			name:     "Empty string",
			input:    "",
			expected: "",
		},
		{
			name:     "Valid string",
			input:    "Great power, great irresponsibility",
			expected: "Great power, great irresponsibility",
		},
		{
			name:     "String with newline",
			input:    "Great power\n great irresponsibility\n",
			expected: "Great power  great irresponsibility ",
		},
		{
			name:     "String with control characters",
			input:    "Great power\x01, great\x0F irresponsibility",
			expected: "Great power , great  irresponsibility",
		},
		{
			name:     "String with non-characters",
			input:    "Great power\uFDD0, great\uFDEF irresponsibility",
			expected: "Great power , great  irresponsibility",
		},
		{
			name:     "String with other non-characters",
			input:    "Great power\uFFFE\uFFFF, great irresponsibility",
			expected: "Great power  , great irresponsibility",
		},
		{
			name:     "Invalid string",
			input:    "\x01\x7F\uFDD0\uFFFE\uFFFF",
			expected: "     ",
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			result := mqttString(test.input)
			if result != test.expected {
				require.Equal(t, test.expected, result)
			}
		})
	}
}
