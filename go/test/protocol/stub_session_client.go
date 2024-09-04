package protocol

import (
	"context"
	"strings"
	"sync"

	"github.com/eclipse/paho.golang/paho"

	"github.com/Azure/iot-operations-sdks/go/protocol/errors"
	"github.com/Azure/iot-operations-sdks/go/protocol/mqtt"
)

type StubSessionClient struct {
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

func MakeStubSessionClient(clientID string) StubSessionClient {
	return StubSessionClient{
		clientID:                clientID,
		ackedPacketIDs:          make(chan uint16, 10),
		publishedCorrelationIDs: make(chan []byte, 10),
		subscribedTopics:        make(map[string]struct{}),
	}
}

func (c *StubSessionClient) Subscribe(
	ctx context.Context,
	topic string,
	handler mqtt.MessageHandler,
	_ ...mqtt.SubscribeOption,
) (mqtt.Subscription, error) {
	c.subscribedTopics[topic] = struct{}{}

	result := Success
	if len(c.subAckQueue) > 0 {
		result = c.subAckQueue[0]
		c.subAckQueue = c.subAckQueue[1:]
	}

	switch result {
	case Fail:
		return nil, &errors.Error{
			Message: "subscription failed",
			Kind:    errors.MqttError,
		}
	case Drop:
		return nil, &errors.Error{
			Message: "subscribe dropped",
			Kind:    errors.UnknownError,
		}
	}

	c.onPublishReceived = append(c.onPublishReceived,
		func(pb paho.PublishReceived) (bool, error) {
			if isTopicFilterMatch(topic, pb.Packet.Topic) {
				if err := handler(ctx, c.buildMessage(pb.Packet)); err != nil {
					return false, err
				}
				return true, nil
			}
			return false, nil
		})

	return nil, nil
}

func (c *StubSessionClient) Publish(
	_ context.Context,
	topic string,
	payload []byte,
	opts ...mqtt.PublishOption,
) error {
	var opt mqtt.PublishOptions
	opt.Apply(opts)

	payloadFormat := byte(opt.PayloadFormat)
	pub := &paho.Publish{
		QoS:     byte(opt.QoS),
		Retain:  opt.Retain,
		Topic:   topic,
		Payload: payload,
		Properties: &paho.PublishProperties{
			ContentType:     opt.ContentType,
			CorrelationData: opt.CorrelationData,
			PayloadFormat:   &payloadFormat,
			ResponseTopic:   opt.ResponseTopic,
			User:            mapToUserProperties(opt.UserProperties),
		},
	}

	c.publicationCount++
	c.publishedCorrelationIDs <- opt.CorrelationData
	c.publishedMessages.Store(string(opt.CorrelationData), pub)

	result := Success
	if len(c.pubAckQueue) > 0 {
		result = c.pubAckQueue[0]
		c.pubAckQueue = c.pubAckQueue[1:]
	}

	switch result {
	case Fail:
		return &errors.Error{
			Message: "publication failed",
			Kind:    errors.MqttError,
		}
	case Drop:
		return &errors.Error{
			Message: "publish dropped",
			Kind:    errors.UnknownError,
		}
	default:
		return nil
	}
}

func (c *StubSessionClient) PublicationCount() int {
	return c.publicationCount
}

func (c *StubSessionClient) AcknowledgementCount() int {
	return c.acknowledgementCount
}

func (c *StubSessionClient) ClientID() string {
	return c.clientID
}

func (c *StubSessionClient) enqueuePubAck(ackKind TestAckKind) {
	c.pubAckQueue = append(c.pubAckQueue, ackKind)
}

func (c *StubSessionClient) enqueueSubAck(ackKind TestAckKind) {
	c.subAckQueue = append(c.subAckQueue, ackKind)
}

func (c *StubSessionClient) enqueueUnsubAck(ackKind TestAckKind) {
	c.unsubAckQueue = append(c.unsubAckQueue, ackKind)
}

func (c *StubSessionClient) getNewPacketID() uint16 {
	c.packetIDSequencer++
	return c.packetIDSequencer
}

func (c *StubSessionClient) receiveMessage(p *paho.Publish) {
	for _, h := range c.onPublishReceived {
		handled, _ := h(paho.PublishReceived{Packet: p})
		if handled {
			return
		}
	}

	// auto-ack when not claimed by any handler
	c.acknowledgementCount++
	c.ackedPacketIDs <- p.PacketID
}

func (c *StubSessionClient) awaitAcknowledgement() uint16 {
	return <-c.ackedPacketIDs
}

func (c *StubSessionClient) awaitPublish() []byte {
	return <-c.publishedCorrelationIDs
}

func (c *StubSessionClient) hasSubscribed(topic string) bool {
	_, ok := c.subscribedTopics[topic]
	return ok
}

func (c *StubSessionClient) getPublishedMessage(
	correlationData []byte,
) (*paho.Publish, bool) {
	val, ok := c.publishedMessages.Load(string(correlationData))
	if !ok {
		return nil, false
	}

	pub, ok := val.(*paho.Publish)
	return pub, ok
}

func (c *StubSessionClient) buildMessage(p *paho.Publish) *mqtt.Message {
	msg := &mqtt.Message{
		Topic:   p.Topic,
		Payload: p.Payload,
		PublishOptions: mqtt.PublishOptions{
			ContentType:     p.Properties.ContentType,
			CorrelationData: p.Properties.CorrelationData,
			QoS:             mqtt.QoS(p.QoS),
			ResponseTopic:   p.Properties.ResponseTopic,
			Retain:          p.Retain,
			UserProperties:  userPropertiesToMap(p.Properties.User),
		},
		Ack: func() error {
			c.acknowledgementCount++
			c.ackedPacketIDs <- p.PacketID
			return nil
		},
	}
	if p.Properties.MessageExpiry != nil {
		msg.MessageExpiry = *p.Properties.MessageExpiry
	}
	if p.Properties.PayloadFormat != nil {
		msg.PayloadFormat = mqtt.PayloadFormat(*p.Properties.PayloadFormat)
	}
	return msg
}

func isTopicFilterMatch(topicFilter, topicName string) bool {
	const sharedPrefix = "$share/"

	// Handle shared subscriptions
	if strings.HasPrefix(topicFilter, sharedPrefix) {
		// Find the index of the second slash
		secondSlashIdx := strings.Index(topicFilter[len(sharedPrefix):], "/")
		if secondSlashIdx == -1 {
			// Invalid shared subscription format
			return false
		}
		topicFilter = topicFilter[len(sharedPrefix)+secondSlashIdx+1:]
	}

	// Return false if the multi-level wildcard is not at the end
	if strings.Contains(topicFilter, "#") &&
		!strings.HasSuffix(topicFilter, "/#") {
		return false
	}

	filters := strings.Split(topicFilter, "/")
	names := strings.Split(topicName, "/")

	for i, filter := range filters {
		if filter == "#" {
			// Multi-level wildcard must be at the end
			return i == len(filters)-1
		}
		if filter == "+" {
			// Single-level wildcard matches any single level
			continue
		}
		if i >= len(names) || filter != names[i] {
			return false
		}
	}

	// Exact match is required if there are no wildcards left
	return len(filters) == len(names)
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
