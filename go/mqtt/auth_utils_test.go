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

// Mock function to create an encrypted PEM block for testing.
func createEncryptedPEMBlock(
	password []byte,
) (*pem.Block, []byte, error) {
	// Create a random salt.
	salt := make([]byte, 8)
	_, err := rand.Read(salt)
	if err != nil {
		return nil, nil, err
	}

	// Derive key using PBKDF2.
	key := pbkdf2.Key(password, salt, 10000, 32, sha3.New256)

	// Create a random nonce.
	nonce := make([]byte, 12)
	_, err = rand.Read(nonce)
	if err != nil {
		return nil, nil, err
	}

	// Create a new AES cipher.
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, nil, err
	}

	gcm, err := cipher.NewGCM(block)
	if err != nil {
		return nil, nil, err
	}

	// Create random plaintext.
	plaintext := []byte("spongebob")

	// Encrypt the plaintext.
	ciphertext := gcm.Seal(nil, nonce, plaintext, nil)

	// Combine salt, nonce, and ciphertext.
	encrypted := salt
	encrypted = append(encrypted, nonce...)
	encrypted = append(encrypted, ciphertext...)

	// Create a PEM block.
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
			Bytes: block.Bytes[:19], // Too short ciphertext.
		}
		_, err := decryptPEMBlock(invalidBlock, password)
		require.Error(
			t,
			errors.New("ciphertext in PEM block is too short"),
			err,
		)
	})
}
