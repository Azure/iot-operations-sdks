package mqtt

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/mqtt/internal"
	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/eclipse/paho.golang/paho"
)

var (
	authErr = internal.ErrMap[paho.AuthResponse]{
		String: "MQTT authentication",
		Reason: func(r *paho.AuthResponse) (byte, string) {
			return r.ReasonCode, r.Properties.ReasonString
		},
	}
	pubErr = internal.ErrMap[paho.PublishResponse]{
		String: "MQTT publish",
		Reason: func(r *paho.PublishResponse) (byte, string) {
			// Paho could possibly return empty PublishResponse struct.
			if r.Properties == nil {
				return r.ReasonCode, ""
			}
			return r.ReasonCode, r.Properties.ReasonString
		},
	}
	subErr = internal.ErrMap[paho.Suback]{
		String: "MQTT subscribe",
		Reason: func(a *paho.Suback) (byte, string) {
			return a.Reasons[0], a.Properties.ReasonString
		},
	}
	unsubErr = internal.ErrMap[paho.Unsuback]{
		String: "MQTT unsubscribe",
		Reason: func(a *paho.Unsuback) (byte, string) {
			return a.Reasons[0], a.Properties.ReasonString
		},
	}
	connErr = internal.ErrMap[paho.Connack]{
		String: "MQTT connect",
		Reason: func(a *paho.Connack) (byte, string) {
			return a.ReasonCode, a.Properties.ReasonString
		},
	}
	disconnErr = internal.ErrMap[paho.Disconnect]{
		String: "MQTT disconnect",
		Reason: func(a *paho.Disconnect) (byte, string) {
			return a.ReasonCode, a.Properties.ReasonString
		},
	}
)

func pahoConn(
	ctx context.Context,
	c PahoClient,
	p *paho.Connect,
) (*paho.Connack, error) {
	res, err := c.Connect(ctx, p)
	if e := connErr.Translate(ctx, res, err); e != nil {
		return nil, e
	}
	return res, nil
}

func pahoDisconn(
	c PahoClient,
	p *paho.Disconnect,
) error {
	return disconnErr.Translate(context.Background(), p, c.Disconnect(p))
}

func pahoSub(
	ctx context.Context,
	c PahoClient,
	p *paho.Subscribe,
) error {
	res, err := c.Subscribe(ctx, p)
	return subErr.Translate(ctx, res, err)
}

func pahoUnsub(
	ctx context.Context,
	c PahoClient,
	p *paho.Unsubscribe,
) error {
	res, err := c.Unsubscribe(ctx, p)
	return unsubErr.Translate(ctx, res, err)
}

func pahoPub(
	ctx context.Context,
	c PahoClient,
	p *paho.Publish,
) error {
	res, err := c.Publish(ctx, p)

	// TODO: There is a bug (https://github.com/eclipse/paho.golang/pull/255)
	// in Paho, which could send (nil, nil) for QoS 0.
	// It has been fixed in Paho but not yet released in version 0.21.
	if p.QoS == 0 && res == nil && err == nil {
		return nil
	}

	return pubErr.Translate(ctx, res, err)
}

func pahoAck(
	c PahoClient,
	p *paho.Publish,
) error {
	return errors.Normalize(c.Ack(p), "MQTT ack")
}

func pahoAuth(
	ctx context.Context,
	c PahoClient,
	p *paho.Auth,
) error {
	res, err := c.Authenticate(ctx, p)
	return authErr.Translate(ctx, res, err)
}
