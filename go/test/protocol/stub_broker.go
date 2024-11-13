// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"net"
	"sync"

	"github.com/Azure/iot-operations-sdks/go/internal/container"
	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/eclipse/paho.golang/packets"
)

type StubBroker struct {
	PublicationCount     int
	AcknowledgementCount int

	packetIDSequencer uint16

	pubAckQueue   []TestAckKind
	subAckQueue   []TestAckKind
	unsubAckQueue []TestAckKind

	ackedPacketIDs          chan uint16
	publishedCorrelationIDs chan []byte
	subscribedTopics        map[string]struct{}
	publishedMessages       container.SyncMap[string, *packets.Publish]
	packets                 chan *packets.ControlPacket

	client net.Conn
	server net.Conn
	mu     sync.RWMutex
}

func NewStubBroker() (*StubBroker, mqtt.ConnectionProvider) {
	s := &StubBroker{
		ackedPacketIDs:          make(chan uint16, 10),
		publishedCorrelationIDs: make(chan []byte, 10),
		subscribedTopics:        make(map[string]struct{}),
		publishedMessages:       container.NewSyncMap[string, *packets.Publish](),
		packets:                 make(chan *packets.ControlPacket, 10),
	}
	s.Disconnect() // To spin up the initial pipe.

	go func() {
		for packet := range s.packets {
			switch p := packet.Content.(type) {
			case *packets.Connect:
				s.connect(p)
			case *packets.Puback:
				s.puback(p)
			case *packets.Publish:
				s.publish(p)
			case *packets.Subscribe:
				s.subscribe(p)
			case *packets.Unsubscribe:
				s.unsubscribe(p)
			case *packets.Auth:
				s.auth(p)
			}
		}
	}()

	return s, func(context.Context) (net.Conn, error) {
		s.mu.RLock()
		defer s.mu.RUnlock()
		return s.client, nil
	}
}

func (s *StubBroker) packet(p packets.Packet) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	_, err := p.WriteTo(s.server)
	if err != nil {
		panic(err)
	}
}

func (s *StubBroker) drop() {
	s.mu.RLock()
	defer s.mu.RUnlock()

	// Just something that will fail Paho.
	_, err := s.server.Write([]byte("unspecified error"))
	if err != nil {
		panic(err)
	}
}

func (s *StubBroker) connect(*packets.Connect) {
	s.packet(&packets.Connack{
		Properties:     &packets.Properties{ReasonString: "OK"},
		ReasonCode:     0,
		SessionPresent: true,
	})
}

func (s *StubBroker) puback(ack *packets.Puback) {
	s.AcknowledgementCount++
	s.ackedPacketIDs <- ack.PacketID
}

func (s *StubBroker) publish(pub *packets.Publish) {
	s.PublicationCount++
	s.packetIDSequencer = pub.PacketID

	s.publishedCorrelationIDs <- pub.Properties.CorrelationData
	s.publishedMessages.Set(string(pub.Properties.CorrelationData), pub)

	result := Success
	if len(s.pubAckQueue) > 0 {
		result = s.pubAckQueue[0]
		s.pubAckQueue = s.pubAckQueue[1:]
	}

	switch result {
	case Fail:
		s.packet(&packets.Puback{
			PacketID:   pub.PacketID,
			ReasonCode: byte(0x80),
			Properties: &packets.Properties{ReasonString: "some reason"},
		})
	case Drop:
		s.drop()
	default:
		s.packet(&packets.Puback{
			PacketID:   pub.PacketID,
			ReasonCode: byte(0),
			Properties: &packets.Properties{ReasonString: "OK"},
		})
	}
}

func (s *StubBroker) subscribe(sub *packets.Subscribe) {
	s.packetIDSequencer = sub.PacketID
	for _, sub := range sub.Subscriptions {
		s.subscribedTopics[sub.Topic] = struct{}{}
	}

	result := Success
	if len(s.subAckQueue) > 0 {
		result = s.subAckQueue[0]
		s.subAckQueue = s.subAckQueue[1:]
	}

	switch result {
	case Fail:
		s.packet(&packets.Suback{
			PacketID:   sub.PacketID,
			Reasons:    []byte{0x80},
			Properties: &packets.Properties{ReasonString: "some reason"},
		})
	case Drop:
		s.drop()
	default:
		s.packet(&packets.Suback{
			PacketID:   sub.PacketID,
			Reasons:    []byte{0},
			Properties: &packets.Properties{ReasonString: "OK"},
		})
	}
}

func (s *StubBroker) unsubscribe(unsub *packets.Unsubscribe) {
	s.packetIDSequencer = unsub.PacketID

	result := Success
	if len(s.unsubAckQueue) > 0 {
		result = s.unsubAckQueue[0]
		s.unsubAckQueue = s.unsubAckQueue[1:]
	}

	switch result {
	case Fail:
		s.packet(&packets.Unsuback{
			PacketID:   unsub.PacketID,
			Reasons:    []byte{0x80},
			Properties: &packets.Properties{ReasonString: "some reason"},
		})
	case Drop:
		s.drop()
	default:
		s.packet(&packets.Unsuback{
			PacketID:   unsub.PacketID,
			Reasons:    []byte{0},
			Properties: &packets.Properties{ReasonString: "OK"},
		})
	}
}

func (s *StubBroker) auth(*packets.Auth) {
	result := Success

	switch result {
	case Fail:
		s.packet(&packets.Auth{
			ReasonCode: byte(0x80),
			Properties: &packets.Properties{ReasonString: "some reason"},
		})
	case Drop:
		s.drop()
	default:
		s.packet(&packets.Auth{
			ReasonCode: byte(0),
			Properties: &packets.Properties{ReasonString: "OK"},
		})
	}
}

func (s *StubBroker) EnqueuePubAck(ackKind TestAckKind) {
	s.pubAckQueue = append(s.pubAckQueue, ackKind)
}

func (s *StubBroker) EnqueueSubAck(ackKind TestAckKind) {
	s.subAckQueue = append(s.subAckQueue, ackKind)
}

func (s *StubBroker) EnqueueUnsubAck(ackKind TestAckKind) {
	s.unsubAckQueue = append(s.unsubAckQueue, ackKind)
}

func (s *StubBroker) GetNewPacketID() uint16 {
	s.packetIDSequencer++
	return s.packetIDSequencer
}

func (s *StubBroker) ReceiveMessage(p *packets.Publish) {
	s.packet(p)
}

func (s *StubBroker) AwaitAcknowledgement() uint16 {
	return <-s.ackedPacketIDs
}

func (s *StubBroker) AwaitPublish() []byte {
	return <-s.publishedCorrelationIDs
}

func (s *StubBroker) HasSubscribed(topic string) bool {
	_, ok := s.subscribedTopics[topic]
	return ok
}

func (s *StubBroker) GetPublishedMessage(
	correlationData []byte,
) (*packets.Publish, bool) {
	return s.publishedMessages.Get(string(correlationData))
}

func (s *StubBroker) Disconnect() {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.client != nil {
		s.client.Close()
	}

	client, server := net.Pipe()
	s.client = packets.NewThreadSafeConn(client)
	s.server = packets.NewThreadSafeConn(server)

	go func() {
		for {
			recv, err := packets.ReadPacket(server)
			if err != nil {
				return
			}
			s.packets <- recv
		}
	}()
}
