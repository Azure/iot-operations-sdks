package protocol_test

import (
	"context"
	"fmt"
	"net"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
	"github.com/eclipse/paho.golang/paho"
	mochi "github.com/mochi-mqtt/server/v2"
	"github.com/mochi-mqtt/server/v2/hooks/auth"
	"github.com/mochi-mqtt/server/v2/listeners"
	"github.com/stretchr/testify/require"
)

type (
	mqttStub struct {
		Client mqtt.Client
		Server mqtt.Client
		Broker *mochi.Server
	}

	clientStub struct {
		client *paho.Client
		id     string
	}

	subStub struct {
		client *paho.Client
		topic  string
		remove func()
	}
)

// Spin up an in-process MQTT broker for testing and connects two clients to it.
func setupMqtt(ctx context.Context, t *testing.T, port int) *mqttStub {
	cfg := listeners.Config{
		Type:    "tcp",
		Address: fmt.Sprintf(":%d", port),
	}
	broker := mochi.New(nil)

	err := broker.AddHook(&auth.AllowHook{}, nil)
	require.NoError(t, err)

	err = broker.AddListener(listeners.NewTCP(cfg))
	require.NoError(t, err)

	err = broker.Serve()
	require.NoError(t, err)

	client := newClientStub(ctx, t, "client", cfg)
	server := newClientStub(ctx, t, "server", cfg)

	return &mqttStub{client, server, broker}
}

func newClientStub(
	ctx context.Context,
	t *testing.T,
	id string,
	cfg listeners.Config,
) mqtt.Client {
	var d net.Dialer
	conn, err := d.DialContext(ctx, cfg.Type, cfg.Address)
	require.NoError(t, err)

	client := paho.NewClient(paho.ClientConfig{
		ClientID:                   id,
		EnableManualAcknowledgment: true,
		Conn:                       conn,
	})

	_, err = client.Connect(ctx, &paho.Connect{
		ClientID:  client.ClientID(),
		KeepAlive: 5,
	})
	require.NoError(t, err)

	return &clientStub{client, id}
}

func (c clientStub) Subscribe(
	ctx context.Context,
	topic string,
	handler mqtt.MessageHandler,
	opts ...mqtt.SubscribeOption,
) (mqtt.Subscription, error) {
	var o mqtt.SubscribeOptions
	o.Apply(opts)

	remove := c.client.AddOnPublishReceived(
		func(pub paho.PublishReceived) (bool, error) {
			p := pub.Packet
			prop := p.Properties
			return true, handler(ctx, &mqtt.Message{
				Topic:   p.Topic,
				Payload: p.Payload,
				PublishOptions: mqtt.PublishOptions{
					ContentType:     prop.ContentType,
					CorrelationData: prop.CorrelationData,
					MessageExpiry:   *prop.MessageExpiry,
					PayloadFormat:   mqtt.PayloadFormat(*prop.PayloadFormat),
					QoS:             mqtt.QoS(p.QoS),
					ResponseTopic:   prop.ResponseTopic,
					Retain:          p.Retain,
					UserProperties:  userPropertiesToMap(prop.User),
				},
				Ack: func() error { return c.client.Ack(p) },
			})
		},
	)
	_, err := c.client.Subscribe(ctx, &paho.Subscribe{
		Properties: &paho.SubscribeProperties{
			User: mapToUserProperties(o.UserProperties),
		},
		Subscriptions: []paho.SubscribeOptions{{
			Topic:             topic,
			QoS:               byte(o.QoS),
			RetainHandling:    byte(o.RetainHandling),
			NoLocal:           o.NoLocal,
			RetainAsPublished: o.Retain,
		}},
	})
	if err != nil {
		remove()
		return nil, err
	}
	return &subStub{c.client, topic, remove}, nil
}

func (c clientStub) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...mqtt.PublishOption,
) error {
	var o mqtt.PublishOptions
	o.Apply(opts)

	payloadFormat := byte(o.PayloadFormat)

	_, err := c.client.Publish(ctx, &paho.Publish{
		QoS:     byte(o.QoS),
		Retain:  o.Retain,
		Topic:   topic,
		Payload: payload,
		Properties: &paho.PublishProperties{
			CorrelationData: o.CorrelationData,
			ContentType:     o.ContentType,
			ResponseTopic:   o.ResponseTopic,
			PayloadFormat:   &payloadFormat,
			MessageExpiry:   &o.MessageExpiry,
			User:            mapToUserProperties(o.UserProperties),
		},
	})
	return err
}

func (c clientStub) ClientID() string {
	return c.id
}

func (s subStub) Unsubscribe(
	ctx context.Context,
	opts ...mqtt.UnsubscribeOption,
) error {
	var o mqtt.UnsubscribeOptions
	o.Apply(opts)
	unsub := &paho.Unsubscribe{Topics: []string{s.topic}}
	if len(o.UserProperties) != 0 {
		unsub.Properties = &paho.UnsubscribeProperties{
			User: mapToUserProperties(o.UserProperties),
		}
	}

	_, err := s.client.Unsubscribe(ctx, unsub)
	if err == nil {
		s.remove()
	}
	return err
}

func userPropertiesToMap(ups paho.UserProperties) map[string]string {
	m := make(map[string]string, len(ups))
	for _, prop := range ups {
		m[prop.Key] = prop.Value
	}
	return m
}

func mapToUserProperties(m map[string]string) paho.UserProperties {
	ups := make(paho.UserProperties, 0, len(m))
	for key, value := range m {
		ups = append(ups, paho.UserProperty{Key: key, Value: value})
	}
	return ups
}
