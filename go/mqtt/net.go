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

// TCPConnection is a ConnectionProvider that connects to an MQTT server over
// TCP.
func TCPConnection(hostname string, port int) ConnectionProvider {
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

// TLSConfigProvider is a function that returns a *tls.Config to be used when
// opening a TLS connection to an MQTT server. See tls.Config for more
// information on TLS configuration options.
type TLSConfigProvider func(context.Context) (*tls.Config, error)

// ConstantTLSConfig is a TLSConfigProvider that returns an unchanging
// *tls.Config. This can be used if the TLS configuration does not need to be
// updated between network connections to the MQTT server.
func ConstantTLSConfig(config *tls.Config) TLSConfigProvider {
	return func(context.Context) (*tls.Config, error) {
		return config, nil
	}
}

// TLSConnectionProviderWithConfigProvider is a ConnectionProvider that
// connects to an MQTT server with TLS over TCP given a TLSConfigProvider.
// This is an advanced option that most users will not need to use. Consider
// using TLSConnectionProviderWithConfig instead.
func TLSConnection(
	hostname string,
	port int,
	tlsConfigProvider TLSConfigProvider,
) ConnectionProvider {
	return func(ctx context.Context) (net.Conn, error) {
		if tlsConfigProvider == nil {
			// use the zero configuration by default
			tlsConfigProvider = ConstantTLSConfig(nil)
		}

		config, err := tlsConfigProvider(ctx)
		if err != nil {
			return nil, &ConnectionError{
				message: "error getting TLS configuration",
				wrapped: err,
			}
		}

		d := tls.Dialer{Config: config}
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
		return packets.NewThreadSafeConn(conn), nil
	}
}
