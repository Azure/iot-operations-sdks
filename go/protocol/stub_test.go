// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol_test

import (
	"context"
	"fmt"
	"net"
	"sync"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/internal/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/eclipse/paho.golang/paho"
	mochi "github.com/mochi-mqtt/server/v2"
	"github.com/mochi-mqtt/server/v2/hooks/auth"
	"github.com/mochi-mqtt/server/v2/listeners"
	"github.com/stretchr/testify/require"
)

type (
	mqttStub struct {
		Client protocol.MqttClient
		Server protocol.MqttClient
		Broker *mochi.Server
	}

	clientStub struct {
		client *paho.Client
		id     string

		handlers []mqtt.MessageHandler
		mu       sync.RWMutex
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
) protocol.MqttClient {
	var d net.Dialer
	conn, err := d.DialContext(ctx, cfg.Type, cfg.Address)
	require.NoError(t, err)

	c := &clientStub{id: id}

	c.client = paho.NewClient(paho.ClientConfig{
		ClientID:                   id,
		EnableManualAcknowledgment: true,
		Conn:                       conn,
		OnPublishReceived: []func(paho.PublishReceived) (bool, error){
			func(pub paho.PublishReceived) (bool, error) {
				c.mu.RLock()
				defer c.mu.RUnlock()

				p := pub.Packet
				prop := p.Properties
				msg := &mqtt.Message{
					Topic:   p.Topic,
					Payload: p.Payload,
					PublishOptions: mqtt.PublishOptions{
						ContentType:     prop.ContentType,
						CorrelationData: prop.CorrelationData,
						MessageExpiry:   *prop.MessageExpiry,
						PayloadFormat:   *prop.PayloadFormat,
						QoS:             p.QoS,
						ResponseTopic:   prop.ResponseTopic,
						Retain:          p.Retain,
						UserProperties:  userPropertiesToMap(prop.User),
					},
					Ack: func() error { return c.client.Ack(p) },
				}

				for _, handle := range c.handlers {
					handle(ctx, msg)
				}

				return true, nil
			},
		},
	})

	_, err = c.client.Connect(ctx, &paho.Connect{
		ClientID:  c.client.ClientID(),
		KeepAlive: 5,
	})
	require.NoError(t, err)

	return c
}

func (c *clientStub) RegisterMessageHandler(
	handler mqtt.MessageHandler,
) func() {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.handlers = append(c.handlers, handler)
	return func() {}
}

func (c *clientStub) Publish(
	ctx context.Context,
	topic string,
	payload []byte,
	opts ...mqtt.PublishOption,
) (*mqtt.Ack, error) {
	var o mqtt.PublishOptions
	o.Apply(opts)

	_, err := c.client.Publish(ctx, &paho.Publish{
		QoS:     o.QoS,
		Retain:  o.Retain,
		Topic:   topic,
		Payload: payload,
		Properties: &paho.PublishProperties{
			CorrelationData: o.CorrelationData,
			ContentType:     o.ContentType,
			ResponseTopic:   o.ResponseTopic,
			PayloadFormat:   &o.PayloadFormat,
			MessageExpiry:   &o.MessageExpiry,
			User:            mapToUserProperties(o.UserProperties),
		},
	})

	var zeroValueAck *mqtt.Ack
	if o.QoS == 1 {
		zeroValueAck = &mqtt.Ack{}
	}

	if err != nil {
		return nil, err
	}
	return zeroValueAck, nil
}

func (c *clientStub) ID() string {
	return c.id
}

func (c *clientStub) Subscribe(
	ctx context.Context,
	topic string,
	opts ...mqtt.SubscribeOption,
) (*mqtt.Ack, error) {
	var o mqtt.SubscribeOptions
	o.Apply(opts)

	_, err := c.client.Subscribe(ctx, &paho.Subscribe{
		Properties: &paho.SubscribeProperties{
			User: mapToUserProperties(o.UserProperties),
		},
		Subscriptions: []paho.SubscribeOptions{{
			Topic:             topic,
			QoS:               o.QoS,
			RetainHandling:    o.RetainHandling,
			NoLocal:           o.NoLocal,
			RetainAsPublished: o.Retain,
		}},
	})
	if err != nil {
		return nil, err
	}
	return &mqtt.Ack{}, nil
}

func (c *clientStub) Unsubscribe(
	ctx context.Context,
	topic string,
	opts ...mqtt.UnsubscribeOption,
) (*mqtt.Ack, error) {
	var o mqtt.UnsubscribeOptions
	o.Apply(opts)
	unsub := &paho.Unsubscribe{Topics: []string{topic}}
	if len(o.UserProperties) != 0 {
		unsub.Properties = &paho.UnsubscribeProperties{
			User: mapToUserProperties(o.UserProperties),
		}
	}

	_, err := c.client.Unsubscribe(ctx, unsub)
	if err != nil {
		return nil, err
	}
	return &mqtt.Ack{}, nil
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
