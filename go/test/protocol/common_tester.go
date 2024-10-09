package protocol

import (
	"context"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol/wallclock"
	"github.com/eclipse/paho.golang/paho"
	"github.com/stretchr/testify/require"
)

func getStubAndSessionClient(
	t *testing.T,
	clientID string,
	useRealSession bool,
) (stubClient StubClient, sessionClient mqtt.Client) {
	if useRealSession {
		mqttClient := MakeStubMqttClient(clientID)
		stubClient = &mqttClient
		sessionClientRaw, err := mqtt.NewSessionClient(
			"tcp://localhost:1234",
			mqtt.WithPahoClientFactory(
				func(*paho.ClientConfig) mqtt.PahoClient {
					return &mqttClient
				},
			),
			mqtt.WithPahoClientConfig(&paho.ClientConfig{}),
			mqtt.WithClientID(clientID),
		)
		require.NoError(t, err)
		err = sessionClientRaw.Connect(context.Background())
		require.NoError(t, err)
		sessionClient = sessionClientRaw
	} else {
		mqttClient := MakeStubSessionClient(clientID)
		stubClient = &mqttClient
		sessionClient = &mqttClient
	}

	return stubClient, sessionClient
}

func awaitAcknowledgement(
	t *testing.T,
	actionAwaitAck *TestCaseActionAwaitAck,
	mqttClient StubClient,
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
	mqttClient StubClient,
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
