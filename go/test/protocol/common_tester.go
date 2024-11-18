// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/eclipse/paho.golang/packets"
	"github.com/stretchr/testify/require"
)

func getStubAndSessionClient(
	t *testing.T,
	clientID string,
) (*StubBroker, protocol.MqttClient) {
	stubBroker, provider := NewStubBroker()
	sessionClient := mqtt.NewSessionClient(
		provider,
		mqtt.WithClientID(clientID),
	)
	require.NoError(t, sessionClient.Start())
	return stubBroker, sessionClient
}

func awaitAcknowledgement(
	t *testing.T,
	actionAwaitAck *TestCaseActionAwaitAck,
	stubBroker *StubBroker,
	packetIDs map[int]uint16,
) {
	packetID := stubBroker.AwaitAcknowledgement()

	if actionAwaitAck.PacketIndex != nil {
		extantPacketID, ok := packetIDs[*actionAwaitAck.PacketIndex]
		require.True(
			t,
			ok,
			"PacketIndex %d not found",
			*actionAwaitAck.PacketIndex,
		)
		require.Equal(t, extantPacketID, packetID)
	}
}

func awaitPublish(
	_ *testing.T,
	actionAwaitPublish *TestCaseActionAwaitPublish,
	stubBroker *StubBroker,
	correlationIDs map[int][]byte,
) {
	correlationID := stubBroker.AwaitPublish()

	if actionAwaitPublish.CorrelationIndex != nil {
		correlationIDs[*actionAwaitPublish.CorrelationIndex] = correlationID
	}
}

func sleep(actionSleep *TestCaseActionSleep) {
	time.Sleep(actionSleep.Duration.ToDuration())
}

func freezeTime() int {
	if f, ok := wallclock.Instance.(*freezableWallClock); ok {
		return f.freezeTime()
	}
	return -1
}

func unfreezeTime(ticket int) {
	if f, ok := wallclock.Instance.(*freezableWallClock); ok {
		f.unfreezeTime(ticket)
	}
}

func getUserProperty(
	_ *testing.T,
	msg *packets.Publish,
	key string,
) (string, bool) {
	for _, kvp := range msg.Properties.User {
		if kvp.Key == key {
			return kvp.Value, true
		}
	}

	return "", false
}
