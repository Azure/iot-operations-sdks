// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"crypto/tls"
	"fmt"
	"net"

	"github.com/eclipse/paho.golang/packets"
)

// ConnectionProvider is a function that returns a net.Conn connected to an
// MQTT server that is ready to read to and write from. Note that the returned
// net.Conn must be thread-safe (i.e., concurrent Write calls must not
// interleave).
type ConnectionProvider func(context.Context) (net.Conn, error)

// TCPConnection is a connection provider that connects to an MQTT server over
// TCP.
func TCPConnection(hostname string, port uint16) ConnectionProvider {
	return func(ctx context.Context) (net.Conn, error) {
		var d net.Dialer
		conn, err := d.DialContext(
			ctx,
			"tcp",
			fmt.Sprintf("%s:%d", hostname, port),
		)
		if err != nil {
			return nil, &ConnectionError{
				message: "error opening TCP connection",
				wrapped: err,
			}
		}
		return conn, nil
	}
}

// TLSOption is a function that modifies a tls.Config to be used when opening
// a TLS connection to an MQTT server. More than one can be provided to
// TLSConnection; they will be executed in order, with the first passed the
// empty (default) TLS config. See tls.Config for more information on TLS
// configuration options.
type TLSOption func(context.Context, *tls.Config) error

// WithX509 appends an X509 certificate to the TLS certificates.
func WithX509(certFile, keyFile string) TLSOption {
	return func(_ context.Context, cfg *tls.Config) error {
		cert, err := tls.LoadX509KeyPair(certFile, keyFile)
		if err != nil {
			return err
		}
		cfg.Certificates = append(cfg.Certificates, cert)
		return nil
	}
}

// WithEncryptedX509 appends an X509 certificate to the TLS certificates, using
// a password file to decrypt the certificate key.
func WithEncryptedX509(certFile, keyFile, passFile string) TLSOption {
	return func(_ context.Context, cfg *tls.Config) error {
		cert, err := loadX509KeyPairWithPassword(certFile, keyFile, passFile)
		if err != nil {
			return err
		}
		cfg.Certificates = append(cfg.Certificates, cert)
		return nil
	}
}

// WithCA loads a CA certificate pool into the root CAs of the TLS
// configuration.
func WithCA(caFile string) TLSOption {
	return func(_ context.Context, cfg *tls.Config) error {
		certPool, err := loadCACertPool(caFile)
		if err != nil {
			return err
		}
		cfg.RootCAs = certPool
		return nil
	}
}

// TLSConnection is a connection provider that connects to an MQTT server with
// TLS over TCP.
func TLSConnection(
	hostname string,
	port uint16,
	opts ...TLSOption,
) ConnectionProvider {
	return func(ctx context.Context) (net.Conn, error) {
		tlsConfig := &tls.Config{MinVersion: tls.VersionTLS12}
		for _, opt := range opts {
			if err := opt(ctx, tlsConfig); err != nil {
				return nil, &ConnectionError{
					message: "error getting TLS configuration",
					wrapped: err,
				}
			}
		}

		d := tls.Dialer{Config: tlsConfig}
		conn, err := d.DialContext(
			ctx,
			"tcp",
			fmt.Sprintf("%s:%d", hostname, port),
		)
		if err != nil {
			return nil, &ConnectionError{
				message: "error opening TLS connection",
				wrapped: err,
			}
		}
		// https://github.com/golang/go/issues/27203
		return packets.NewThreadSafeConn(conn), nil
	}
}
