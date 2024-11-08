// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"errors"
	"sync"

	"github.com/eclipse/paho.golang/paho"
)

type StubMqttClient struct {
	publicationCount        int
	acknowledgementCount    int
	clientID                string
	packetIDSequencer       uint16
	pubAckQueue             []TestAckKind
	subAckQueue             []TestAckKind
	unsubAckQueue           []TestAckKind
	ackedPacketIDs          chan uint16
	publishedCorrelationIDs chan []byte
	subscribedTopics        map[string]struct{}
	publishedMessages       sync.Map
	onPublishReceived       []func(paho.PublishReceived) (bool, error)
}

func MakeStubMqttClient(clientID string) *StubMqttClient {
	return &StubMqttClient{
		clientID:                clientID,
		ackedPacketIDs:          make(chan uint16, 10),
		publishedCorrelationIDs: make(chan []byte, 10),
		subscribedTopics:        make(map[string]struct{}),
	}
}

func (*StubMqttClient) Connect(
	_ context.Context,
	_ *paho.Connect,
) (*paho.Connack, error) {
	return &paho.Connack{
		Properties: &paho.ConnackProperties{
			ReasonString: "OK",
		},
		ReasonCode:     0,
		SessionPresent: true,
	}, nil
}

func (*StubMqttClient) Disconnect(
	_ *paho.Disconnect,
) error {
	return nil
}

func (c *StubMqttClient) Ack(pb *paho.Publish) error {
	c.acknowledgementCount++
	c.ackedPacketIDs <- pb.PacketID
	return nil
}

func (c *StubMqttClient) AddOnPublishReceived(
	f func(paho.PublishReceived) (bool, error),
) func() {
	c.onPublishReceived = append(c.onPublishReceived, f)
	return func() {}
}

func (c *StubMqttClient) PublicationCount() int {
	return c.publicationCount
}

func (c *StubMqttClient) AcknowledgementCount() int {
	return c.acknowledgementCount
}

func (c *StubMqttClient) ClientID() string {
	return c.clientID
}

func (c *StubMqttClient) PublishWithOptions(
	_ context.Context,
	p *paho.Publish,
	_ paho.PublishOptions,
) (*paho.PublishResponse, error) {
	c.publicationCount++

	c.publishedCorrelationIDs <- p.Properties.CorrelationData
	c.publishedMessages.Store(string(p.Properties.CorrelationData), p)

	result := Success
	if len(c.pubAckQueue) > 0 {
		result = c.pubAckQueue[0]
		c.pubAckQueue = c.pubAckQueue[1:]
	}

	switch result {
	case Fail:
		return &paho.PublishResponse{
			ReasonCode: byte(0x80),
			Properties: &paho.PublishResponseProperties{
				ReasonString: "some reason",
			},
		}, nil
	case Drop:
		return nil, errors.New("unspecified error")
	default:
		return &paho.PublishResponse{
			ReasonCode: byte(0),
			Properties: &paho.PublishResponseProperties{ReasonString: "OK"},
		}, nil
	}
}

func (c *StubMqttClient) Subscribe(
	_ context.Context,
	s *paho.Subscribe,
) (*paho.Suback, error) {
	for _, sub := range s.Subscriptions {
		c.subscribedTopics[sub.Topic] = struct{}{}
	}

	result := Success
	if len(c.subAckQueue) > 0 {
		result = c.subAckQueue[0]
		c.subAckQueue = c.subAckQueue[1:]
	}

	switch result {
	case Fail:
		return &paho.Suback{
			Reasons:    []byte{0x80},
			Properties: &paho.SubackProperties{ReasonString: "some reason"},
		}, nil
	case Drop:
		return nil, errors.New("unspecified error")
	default:
		return &paho.Suback{
			Reasons:    []byte{0},
			Properties: &paho.SubackProperties{ReasonString: "OK"},
		}, nil
	}
}

func (c *StubMqttClient) Unsubscribe(
	_ context.Context,
	_ *paho.Unsubscribe,
) (*paho.Unsuback, error) {
	result := Success
	if len(c.unsubAckQueue) > 0 {
		result = c.unsubAckQueue[0]
		c.unsubAckQueue = c.unsubAckQueue[1:]
	}

	switch result {
	case Fail:
		return &paho.Unsuback{
			Reasons:    []byte{0x80},
			Properties: &paho.UnsubackProperties{ReasonString: "some reason"},
		}, nil
	case Drop:
		return nil, errors.New("unspecified error")
	default:
		return &paho.Unsuback{
			Reasons:    []byte{0},
			Properties: &paho.UnsubackProperties{ReasonString: "OK"},
		}, nil
	}
}

func (*StubMqttClient) Authenticate(
	_ context.Context,
	_ *paho.Auth,
) (*paho.AuthResponse, error) {
	result := Success

	switch result {
	case Fail:
		return &paho.AuthResponse{
			ReasonCode: byte(0x80),
			Properties: &paho.AuthProperties{ReasonString: "some reason"},
		}, nil
	case Drop:
		return nil, errors.New("unspecified error")
	default:
		return &paho.AuthResponse{
			ReasonCode: byte(0),
			Properties: &paho.AuthProperties{ReasonString: "OK"},
		}, nil
	}
}

func (c *StubMqttClient) enqueuePubAck(ackKind TestAckKind) {
	c.pubAckQueue = append(c.pubAckQueue, ackKind)
}

func (c *StubMqttClient) enqueueSubAck(ackKind TestAckKind) {
	c.subAckQueue = append(c.subAckQueue, ackKind)
}

func (c *StubMqttClient) enqueueUnsubAck(ackKind TestAckKind) {
	c.unsubAckQueue = append(c.unsubAckQueue, ackKind)
}

func (c *StubMqttClient) getNewPacketID() uint16 {
	c.packetIDSequencer++
	return c.packetIDSequencer
}

func (c *StubMqttClient) receiveMessage(p *paho.Publish) {
	for _, h := range c.onPublishReceived {
		_, _ = h(paho.PublishReceived{Packet: p})
	}
}

func (c *StubMqttClient) awaitAcknowledgement() uint16 {
	return <-c.ackedPacketIDs
}

func (c *StubMqttClient) awaitPublish() []byte {
	return <-c.publishedCorrelationIDs
}

func (c *StubMqttClient) hasSubscribed(topic string) bool {
	_, ok := c.subscribedTopics[topic]
	return ok
}

func (c *StubMqttClient) getPublishedMessage(
	correlationData []byte,
) (*paho.Publish, bool) {
	val, ok := c.publishedMessages.Load(string(correlationData))
	if !ok {
		return nil, false
	}

	pub, ok := val.(*paho.Publish)
	return pub, ok
}
