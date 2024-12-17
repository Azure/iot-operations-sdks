// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package mqtt

import (
	"context"
	"crypto/tls"
	"os"
	"strconv"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/mqtt/auth"
)

type connectionProviderBuilder struct {
	hostname string
	port     uint16
	useTLS   *bool
	caFile   string
	certFile string
	keyFile  string
	passFile string
}

// SessionClientConfigFromEnv parses a session client configuration from
// well-known environment variables. Note that this will only return an error if
// the environment variables parse incorrectly; it will not return an error if
// required parameters (e.g. for the connection provider) are missing, to allow
// optional parameters to be specified from environment independently.
func SessionClientConfigFromEnv() (ConnectionProvider, *SessionClientOptions, error) {
	opts := &SessionClientOptions{}
	conn := connectionProviderBuilder{}

	for _, env := range os.Environ() {
		idx := strings.IndexByte(env, '=')
		key := env[:idx]
		val := env[idx+1:]
		switch key {
		case "AIO_BROKER_HOSTNAME":
			conn.hostname = val

		case "AIO_BROKER_TCP_PORT":
			port, err := strconv.ParseUint(val, 10, 16)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "could not parse broker TCP port",
					wrapped: err,
				}
			}
			conn.port = uint16(port)

		case "AIO_MQTT_USE_TLS":
			useTLS, err := strconv.ParseBool(val)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "could not parse MQTT use TLS",
					wrapped: err,
				}
			}
			conn.useTLS = &useTLS

		case "AIO_MQTT_CLEAN_START":
			cleanStart, err := strconv.ParseBool(val)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "could not parse MQTT clean start",
					wrapped: err,
				}
			}
			opts.CleanStart = cleanStart

		case "AIO_MQTT_KEEP_ALIVE":
			keepAlive, err := strconv.ParseUint(val, 10, 16)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "could not parse MQTT keep-alive",
					wrapped: err,
				}
			}
			opts.KeepAlive = uint16(keepAlive)

		case "AIO_MQTT_CLIENT_ID":
			opts.ClientID = val

		case "AIO_MQTT_SESSION_EXPIRY":
			sessionExpiry, err := strconv.ParseUint(val, 10, 32)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "could not parse MQTT session expiry",
					wrapped: err,
				}
			}
			opts.SessionExpiry = uint32(sessionExpiry)

		case "AIO_MQTT_USERNAME":
			opts.Username = ConstantUsername(val)

		case "AIO_MQTT_PASSWORD_FILE":
			opts.Password = FilePassword(val)

		case "AIO_SAT_FILE":
			satAuth, err := auth.NewAIOServiceAccountToken(val)
			if err != nil {
				return nil, nil, &InvalidArgumentError{
					message: "error setting up the AIO SAT auth provider",
					wrapped: err,
				}
			}
			opts.Auth = satAuth

		case "AIO_TLS_CA_FILE":
			conn.caFile = val

		case "AIO_TLS_CERT_FILE":
			conn.certFile = val

		case "AIO_TLS_KEY_FILE":
			conn.keyFile = val

		case "AIO_TLS_KEY_PASSWORD_FILE":
			conn.passFile = val
		}
	}

	connectionProvider, err := conn.build()
	if err != nil {
		return nil, nil, err
	}
	return connectionProvider, opts, nil
}

// NewSessionClientFromEnv is a shorthand for constructing a session client
// using SessionClientConfigFromEnv.
func NewSessionClientFromEnv(
	opt ...SessionClientOption,
) (*SessionClient, error) {
	connectionProvider, opts, err := SessionClientConfigFromEnv()
	if err != nil {
		return nil, err
	}
	if connectionProvider == nil {
		return nil, &InvalidArgumentError{
			message: "connection must be configured",
		}
	}
	opts.Apply(opt)
	return NewSessionClient(connectionProvider, opts), nil
}

func (b *connectionProviderBuilder) build() (ConnectionProvider, error) {
	if b.hostname == "" {
		if b.port != 0 || b.useTLS != nil || b.hasTLS() {
			return nil, &InvalidArgumentError{
				message: "connection configuration provided without hostname",
			}
		}
		return nil, nil
	}

	if b.port == 0 {
		b.port = 8883
	}

	if b.useTLS != nil && !*b.useTLS {
		if b.hasTLS() {
			return nil, &InvalidArgumentError{
				message: "TLS configuration provided but not using TLS",
			}
		}
		return TCPConnection(b.hostname, b.port), nil
	}

	if (b.certFile != "") != (b.keyFile != "") {
		return nil, &InvalidArgumentError{
			message: "certificate file and key file must be provided together",
		}
	}

	var tlsOpts []TLSOption

	// Bypasses hostname check in TLS config when deliberately connecting to
	// localhost.
	if b.hostname == "localhost" {
		tlsOpts = append(tlsOpts, func(
			_ context.Context,
			cfg *tls.Config,
		) error {
			cfg.InsecureSkipVerify = true // #nosec G402
			return nil
		})
	}

	if b.certFile != "" {
		if b.passFile != "" {
			tlsOpts = append(tlsOpts, WithEncryptedX509(
				b.certFile,
				b.keyFile,
				b.passFile,
			))
		} else {
			tlsOpts = append(tlsOpts, WithX509(
				b.certFile,
				b.keyFile,
			))
		}
	}

	if b.caFile != "" {
		tlsOpts = append(tlsOpts, WithCA(b.caFile))
	}

	return TLSConnection(b.hostname, b.port, tlsOpts...), nil
}

func (b *connectionProviderBuilder) hasTLS() bool {
	return b.caFile != "" || b.certFile != "" ||
		b.keyFile != "" || b.passFile != ""
}
