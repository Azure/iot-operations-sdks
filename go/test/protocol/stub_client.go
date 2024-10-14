// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"github.com/eclipse/paho.golang/paho"
)

type (
	StubClient interface {
		PublicationCount() int

		AcknowledgementCount() int

		ClientID() string

		enqueuePubAck(ackKind TestAckKind)

		enqueueSubAck(ackKind TestAckKind)

		enqueueUnsubAck(ackKind TestAckKind)

		getNewPacketID() uint16

		receiveMessage(p *paho.Publish)

		awaitAcknowledgement() uint16

		awaitPublish() []byte

		hasSubscribed(topic string) bool

		getPublishedMessage(correlationData []byte) (*paho.Publish, bool)
	}
)
