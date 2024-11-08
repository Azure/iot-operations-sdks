// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/internal/wallclock"
	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/eclipse/paho.golang/paho"
	"github.com/stretchr/testify/require"
)

func getStubAndSessionClient(
	t *testing.T,
	clientID string,
) (*StubMqttClient, protocol.MqttClient) {
	mqttClient := MakeStubMqttClient(clientID)
	sessionClient, err := mqtt.NewSessionClient(
		mqtt.TCPConnection("localhost", 1234),
		mqtt.WithPahoConstructor(func(
			_ context.Context,
			cfg *paho.ClientConfig,
		) (mqtt.PahoClient, error) {
			mqttClient.onPublishReceived = cfg.OnPublishReceived
			return mqttClient, nil
		}),
		mqtt.WithClientID(clientID),
	)
	require.NoError(t, err)
	err = sessionClient.Start()
	require.NoError(t, err)

	return mqttClient, sessionClient
}

func awaitAcknowledgement(
	t *testing.T,
	actionAwaitAck *TestCaseActionAwaitAck,
	mqttClient *StubMqttClient,
	packetIDs map[int]uint16,
) {
	packetID := mqttClient.awaitAcknowledgement()

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
	mqttClient *StubMqttClient,
	correlationIDs map[int][]byte,
) {
	correlationID := mqttClient.awaitPublish()

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
	msg *paho.Publish,
	key string,
) (string, bool) {
	for _, kvp := range msg.Properties.User {
		if kvp.Key == key {
			return kvp.Value, true
		}
	}

	return "", false
}
