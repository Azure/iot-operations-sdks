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
	"os"

	"golang.org/x/crypto/pbkdf2"
	"golang.org/x/crypto/sha3"
)

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

	// Extract the salt (first 8 bytes).
	salt := block.Bytes[:8]

	// Derive key using PBKDF2.
	key := pbkdf2.Key(password, salt, 10000, 32, sha3.New256)

	// Decrypt the block using AES-GCM.
	return aesGCMDecrypt(block.Bytes[8:], key)
}

// aesGCMDecrypt decrypts data using AES-GCM mode.
func aesGCMDecrypt(encrypted, key []byte) ([]byte, error) {
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, err
	}

	// AES-GCM typically uses a 12-byte(96-bit) nonce(random number). This size
	// is recommended because it provides a good balance between security and
	// performance.
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
	passFile string,
) (tls.Certificate, error) {
	certPEMBlock, err := os.ReadFile(certFile)
	if err != nil {
		return tls.Certificate{}, err
	}

	keyPEMBlock, err := os.ReadFile(keyFile)
	if err != nil {
		return tls.Certificate{}, err
	}

	password, err := os.ReadFile(passFile)
	if err != nil {
		return tls.Certificate{}, err
	}

	keyDERBlock, _ := pem.Decode(keyPEMBlock)
	if keyDERBlock == nil {
		return tls.Certificate{}, errors.New(
			"failed to decode PEM block containing private key",
		)
	}

	// x509.DecryptPEMBlock is deprecated due to insecurity, and x509 library
	// doesn't want to support it: https://github.com/golang/go/issues/8860
	decryptedDERBlock, err := decryptPEMBlock(keyDERBlock, password)
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
