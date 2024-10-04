package mqtt

import (
	"context"
	"crypto/tls"
	"fmt"
	"net"
	"net/url"

	"github.com/eclipse/paho.golang/packets"
	"github.com/gorilla/websocket"
)

// buildNetConn establishes the network connection
// based on the provided configurations.
func buildNetConn(
	ctx context.Context,
	serverURL string,
	tlsConfig *tls.Config,
) (net.Conn, error) {
	var conn net.Conn
	var err error

	// serverURL parsing error is ignored here,
	// since url has already been validated before during client setup.
	u, _ := url.Parse(serverURL)

	switch u.Scheme {
	case "mqtt", "tcp", "":
		conn, err = buildTCPConnection(ctx, u.Host)
	case "ssl", "tls", "mqtts", "mqtt+ssl", "tcps":
		conn, err = buildTLSConnection(ctx, tlsConfig, u.Host)
	case "ws":
		conn, err = buildWebsocketConnection(ctx, nil, u)
	case "wss":
		conn, err = buildWebsocketConnection(ctx, tlsConfig, u)
	default:
		return nil, &InvalidArgumentError{message: "unsupported URL scheme"}
	}

	if err != nil {
		return nil, retryableErr{fmt.Errorf("MQTT network connection error: %w", err)}
	}
	return conn, nil
}

func buildTCPConnection(
	ctx context.Context,
	address string,
) (net.Conn, error) {
	var d net.Dialer
	conn, err := d.DialContext(ctx, "tcp", address)
	if err != nil {
		return nil, fmt.Errorf("error creating TCP connection: %w", err)
	}
	return conn, nil
}

func buildTLSConnection(
	ctx context.Context,
	tlsCfg *tls.Config,
	address string,
) (net.Conn, error) {
	d := tls.Dialer{
		Config: tlsCfg,
	}
	conn, err := d.DialContext(ctx, "tcp", address)
	if err != nil {
		return nil, fmt.Errorf("error creating TLS connection: %w", err)
	}
	return packets.NewThreadSafeConn(conn), nil
}

func buildWebsocketConnection(
	ctx context.Context,
	tlsCfg *tls.Config,
	serverURL *url.URL,
) (net.Conn, error) {
	d := *websocket.DefaultDialer
	if tlsCfg != nil {
		d.TLSClientConfig = tlsCfg
	}
	// Optional subprotocol setting.
	d.Subprotocols = []string{"mqtt"}

	conn, _, err := d.DialContext(ctx, serverURL.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("error creating websocket connection: %w", err)
	}
	return conn.NetConn(), nil
}
